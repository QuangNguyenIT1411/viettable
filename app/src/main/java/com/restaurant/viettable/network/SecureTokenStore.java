package com.restaurant.viettable.network;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** One atomic encrypted access/refresh pair; no biometric prompt, no exported key material. */
public final class SecureTokenStore {
    private final TokenVault vault;

    public SecureTokenStore(Context context, Runnable invalidated) {
        this(context, "viettable_secure_session", "viettable_session", "viettable.auth.aes.v1", invalidated);
    }

    // Package-private isolation hook: device tests use separate preferences and a separate real Keystore key.
    SecureTokenStore(Context context, String secureName, String legacyName, String alias, Runnable invalidated) {
        SharedPreferences secure = context.getSharedPreferences(secureName, Context.MODE_PRIVATE);
        SharedPreferences legacy = context.getSharedPreferences(legacyName, Context.MODE_PRIVATE);
        vault = new TokenVault(new TokenVault.Persistence() {
            public String blob() { return secure.getString("auth_blob", null); }
            public boolean migrated() { return secure.getBoolean("migration_complete", false); }
            public boolean write(String value, boolean migrated) {
                return secure.edit().putString("auth_blob", value).putBoolean("migration_complete", migrated).commit();
            }
            public TokenVault.Pair legacy() {
                return new TokenVault.Pair(legacy.getString("access_token", ""), legacy.getString("refresh_token", ""));
            }
            public boolean clearLegacy() {
                return legacy.edit().remove("access_token").remove("refresh_token").commit();
            }
        }, new KeystoreEncryption(alias), invalidated);
    }

    public String getAccessToken() { return vault.read().access; }
    public String getRefreshToken() { return vault.read().refresh; }
    public boolean hasSession() { return vault.read().valid(); }
    public boolean save(String access, String refresh) { return vault.save(access, refresh); }
    public void clear() { vault.clear(); }

    private static final class KeystoreEncryption implements TokenVault.Encryption {
        private final String alias;
        KeystoreEncryption(String alias) { this.alias = alias; }
        private KeyStore keyStore() throws Exception {
            KeyStore store = KeyStore.getInstance("AndroidKeyStore");
            store.load(null);
            return store;
        }
        private SecretKey key(boolean create) throws Exception {
            SecretKey existing = (SecretKey) keyStore().getKey(alias, null);
            if (existing != null) return existing;
            if (!create) throw new java.security.KeyStoreException("Missing auth key");
            KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
            generator.init(new KeyGenParameterSpec.Builder(alias,
                    KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                    .setKeySize(256).setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                    .setRandomizedEncryptionRequired(true).setUserAuthenticationRequired(false).build());
            return generator.generateKey();
        }
        public String encrypt(byte[] value) throws Exception {
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, key(true));
            cipher.updateAAD((alias + ":auth_pair:v1").getBytes(StandardCharsets.UTF_8));
            return "v1:" + Base64.encodeToString(cipher.getIV(), Base64.NO_WRAP) + ":"
                    + Base64.encodeToString(cipher.doFinal(value), Base64.NO_WRAP);
        }
        public byte[] decrypt(String value) throws Exception {
            if (value == null || value.length() > 200_000) throw new java.io.IOException();
            String[] parts = value.split(":", -1);
            if (parts.length != 3 || !"v1".equals(parts[0])) throw new java.io.IOException();
            byte[] iv = Base64.decode(parts[1], Base64.NO_WRAP);
            byte[] encrypted = Base64.decode(parts[2], Base64.NO_WRAP);
            if (iv.length != 12 || encrypted.length < 16) throw new java.io.IOException();
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, key(false), new GCMParameterSpec(128, iv));
            cipher.updateAAD((alias + ":auth_pair:v1").getBytes(StandardCharsets.UTF_8));
            return cipher.doFinal(encrypted);
        }
        public void destroy() throws Exception { keyStore().deleteEntry(alias); }
    }
}
