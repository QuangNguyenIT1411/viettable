package com.restaurant.viettable.network;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import com.restaurant.viettable.util.BiometricLoginPolicy;

import java.nio.charset.StandardCharsets;
import java.security.KeyFactory;
import java.security.KeyPairGenerator;
import java.security.KeyStore;
import java.security.PrivateKey;
import java.security.PublicKey;
import java.security.cert.Certificate;
import java.security.spec.MGF1ParameterSpec;
import java.security.spec.X509EncodedKeySpec;

import javax.crypto.Cipher;
import javax.crypto.spec.OAEPParameterSpec;
import javax.crypto.spec.PSource;

/**
 * Fingerprint login, per account and per device. Each account gets its own RSA key in Android Keystore whose
 * private half only works right after a strong fingerprint scan and dies when fingerprints are added or removed.
 * On "Đăng xuất, giữ đăng nhập vân tay" the refresh token is encrypted with the public half (no scan needed);
 * logging in decrypts it inside BiometricPrompt. No password is ever stored. At most one sealed token exists.
 */
public final class BiometricTokenStore {
    private static final String PREFS = "viettable_biometric";
    private static final String ALIAS_PREFIX = "viettable.biometric.rsa.v1.";
    private static final String TRANSFORMATION = "RSA/ECB/OAEPWithSHA-256AndMGF1Padding";
    // Android Keystore only accepts MGF1 with SHA-1 for OAEP decryption.
    private static final OAEPParameterSpec OAEP = new OAEPParameterSpec(
            "SHA-256", "MGF1", MGF1ParameterSpec.SHA1, PSource.PSpecified.DEFAULT);

    private static BiometricTokenStore instance;
    private final SharedPreferences prefs;

    private BiometricTokenStore(Context context) {
        prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    public static synchronized void initialize(Context context) {
        if (instance == null) instance = new BiometricTokenStore(context.getApplicationContext());
    }

    public static BiometricTokenStore getInstance() {
        if (instance == null) throw new IllegalStateException("BiometricTokenStore chưa được khởi tạo.");
        return instance;
    }

    public boolean isEnabled(String accountId) {
        return valid(accountId) && prefs.getBoolean("enabled_" + accountId, false) && keyExists(accountId);
    }

    public boolean wasAsked(String accountId) { return valid(accountId) && prefs.getBoolean("asked_" + accountId, false); }

    public void markAsked(String accountId) {
        if (valid(accountId)) prefs.edit().putBoolean("asked_" + accountId, true).apply();
    }

    /** Creates this account's key; call only after the user confirmed with a fingerprint. */
    public void enable(String accountId) throws Exception {
        if (!valid(accountId)) throw new IllegalArgumentException("account");
        deleteKey(accountId);
        KeyGenParameterSpec.Builder spec = new KeyGenParameterSpec.Builder(alias(accountId), KeyProperties.PURPOSE_DECRYPT)
                .setKeySize(2048)
                .setDigests(KeyProperties.DIGEST_SHA256, KeyProperties.DIGEST_SHA1)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_RSA_OAEP)
                .setUserAuthenticationRequired(true)
                .setInvalidatedByBiometricEnrollment(true);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            spec.setUserAuthenticationParameters(0, KeyProperties.AUTH_BIOMETRIC_STRONG);
        }
        KeyPairGenerator generator = KeyPairGenerator.getInstance(KeyProperties.KEY_ALGORITHM_RSA, "AndroidKeyStore");
        generator.initialize(spec.build());
        generator.generateKeyPair();
        prefs.edit().putBoolean("enabled_" + accountId, true).putBoolean("asked_" + accountId, true).commit();
    }

    /** Turns fingerprint login off for this account: key, sealed token and flag are all removed. */
    public void disable(String accountId) {
        if (!valid(accountId)) return;
        deleteKey(accountId);
        SharedPreferences.Editor edit = prefs.edit().remove("enabled_" + accountId);
        if (accountId.equals(prefs.getString("sealed_owner", null))) clearSealed(edit);
        edit.commit();
    }

    /**
     * Throws KeyPermanentlyInvalidatedException when fingerprints changed since the key was made.
     * Needs no scan: only BiometricPrompt-authenticated use of the cipher does.
     */
    public Cipher decryptCipher(String accountId) throws Exception {
        PrivateKey key = (PrivateKey) keyStore().getKey(alias(accountId), null);
        if (key == null) throw new java.security.KeyStoreException("Missing biometric key");
        Cipher cipher = Cipher.getInstance(TRANSFORMATION);
        cipher.init(Cipher.DECRYPT_MODE, key, OAEP);
        return cipher;
    }

    /** Encrypts the refresh token for a later fingerprint login, replacing any other sealed token. */
    public void seal(String accountId, String label, String refreshToken) throws Exception {
        if (!isEnabled(accountId) || refreshToken == null || refreshToken.isEmpty()) throw new IllegalStateException("not_enabled");
        decryptCipher(accountId); // fail now, not at the next login, if fingerprints already changed
        Certificate certificate = keyStore().getCertificate(alias(accountId));
        PublicKey restricted = certificate.getPublicKey();
        // Keystore public keys carry the auth restriction; a plain copy encrypts without a scan.
        PublicKey publicKey = KeyFactory.getInstance(restricted.getAlgorithm())
                .generatePublic(new X509EncodedKeySpec(restricted.getEncoded()));
        Cipher cipher = Cipher.getInstance(TRANSFORMATION);
        cipher.init(Cipher.ENCRYPT_MODE, publicKey, OAEP);
        String sealed = Base64.encodeToString(cipher.doFinal(refreshToken.getBytes(StandardCharsets.UTF_8)), Base64.NO_WRAP);
        if (!prefs.edit().putString("sealed_owner", accountId).putString("sealed_label", label == null ? "" : label)
                .putString("sealed_token", sealed).commit()) throw new java.io.IOException("prefs");
    }

    /** Account whose token is sealed, or null when fingerprint login is not available on this screen. */
    public String sealedOwner() {
        String owner = prefs.getString("sealed_owner", null);
        return owner != null && prefs.getString("sealed_token", null) != null && isEnabled(owner) ? owner : null;
    }

    public String sealedLabel() { return prefs.getString("sealed_label", ""); }

    /** Decrypts with the cipher BiometricPrompt just unlocked. */
    public String open(Cipher authenticated) throws Exception {
        String sealed = prefs.getString("sealed_token", null);
        if (sealed == null) throw new IllegalStateException("no_sealed_token");
        return new String(authenticated.doFinal(Base64.decode(sealed, Base64.NO_WRAP)), StandardCharsets.UTF_8);
    }

    /**
     * A password/Google login as accountId: the sealed token is superseded by the new session, and every other
     * account's fingerprint data (key, flag, token) is deleted. accountId's own setting is kept.
     */
    public void keepOnlyAccount(String accountId) {
        java.util.List<String> others = new java.util.ArrayList<>();
        for (String key : prefs.getAll().keySet()) {
            if (!key.startsWith("enabled_")) continue;
            String owner = key.substring("enabled_".length());
            if (BiometricLoginPolicy.belongsToOtherAccount(owner, accountId)) others.add(owner);
        }
        for (String owner : others) disable(owner);
        clearSealed();
    }

    /** The sealed token was used (rotated by the server) or must not be used by another account. */
    public void clearSealed() {
        SharedPreferences.Editor edit = prefs.edit();
        clearSealed(edit);
        edit.commit();
    }

    private static void clearSealed(SharedPreferences.Editor edit) {
        edit.remove("sealed_owner").remove("sealed_label").remove("sealed_token");
    }

    private boolean keyExists(String accountId) {
        try { return keyStore().containsAlias(alias(accountId)); }
        catch (Exception ignored) { return false; }
    }

    private void deleteKey(String accountId) {
        try { keyStore().deleteEntry(alias(accountId)); } catch (Exception ignored) { }
    }

    private static KeyStore keyStore() throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        return store;
    }

    private static boolean valid(String accountId) { return accountId != null && !accountId.trim().isEmpty(); }

    private static String alias(String accountId) { return ALIAS_PREFIX + accountId.replaceAll("[^A-Za-z0-9_-]", "_"); }
}
