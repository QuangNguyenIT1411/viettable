package com.restaurant.viettable.network;

import org.junit.Test;
import java.util.*;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;
import static org.junit.Assert.*;

public class TokenVaultTest {
    static class Disk implements TokenVault.Persistence {
        String value;
        boolean done, failWrite, failReadback;
        int legacyReads;
        TokenVault.Pair old = new TokenVault.Pair("legacy-access", "legacy-refresh");
        public String blob() { return failReadback && value != null ? "broken" : value; }
        public boolean migrated() { return done; }
        public boolean write(String blob, boolean migrated) {
            if (failWrite) return false;
            value = blob; done = migrated; return true;
        }
        public TokenVault.Pair legacy() { legacyReads++; return old; }
        public boolean clearLegacy() { old = new TokenVault.Pair("", ""); return true; }
    }
    // Real AES-GCM in JVM tests. Android Keystore integration is exercised on the device separately.
    static class Crypto implements TokenVault.Encryption {
        SecretKey key;
        public String encrypt(byte[] value) throws Exception {
            if (key == null) { KeyGenerator g = KeyGenerator.getInstance("AES"); g.init(256); key = g.generateKey(); }
            Cipher c = Cipher.getInstance("AES/GCM/NoPadding"); c.init(Cipher.ENCRYPT_MODE, key);
            return Base64.getEncoder().encodeToString(c.getIV()) + ":" + Base64.getEncoder().encodeToString(c.doFinal(value));
        }
        public byte[] decrypt(String value) throws Exception {
            String[] p = value.split(":");
            Cipher c = Cipher.getInstance("AES/GCM/NoPadding");
            c.init(Cipher.DECRYPT_MODE, key, new GCMParameterSpec(128, Base64.getDecoder().decode(p[0])));
            return c.doFinal(Base64.getDecoder().decode(p[1]));
        }
        public void destroy() { key = null; }
    }
    @Test public void migrationSurvivesRestartAndRunsOnce() {
        Disk d = new Disk(); Crypto c = new Crypto();
        TokenVault v = new TokenVault(d,c,()->fail("valid migration"));
        assertEquals("legacy-access",v.read().access);
        assertFalse(d.old.valid()); assertTrue(d.done);
        assertFalse(d.value.contains("legacy"));
        new TokenVault(d,c,()->fail("restart"));
        assertEquals(1,d.legacyReads);
    }
    @Test public void failedWritePreservesLegacyButDoesNotAuthenticate() {
        Disk d = new Disk(); d.failWrite = true;
        TokenVault v = new TokenVault(d,new Crypto(),()->{});
        assertFalse(v.read().valid()); assertTrue(d.old.valid()); assertNull(d.value);
    }
    @Test public void failedVerificationPreservesLegacyButDoesNotAuthenticate() {
        Disk d = new Disk(); d.failReadback = true;
        TokenVault v = new TokenVault(d,new Crypto(),()->{});
        assertFalse(v.read().valid()); assertTrue(d.old.valid()); assertFalse(d.done);
    }
    @Test public void corruptionDoesNotFallBackToLegacy() {
        Disk d = new Disk(); Crypto c = new Crypto(); int[] events={0};
        TokenVault v=new TokenVault(d,c,()->events[0]++);
        d.old = new TokenVault.Pair("stale", "stale"); d.value="corrupt";
        assertFalse(v.read().valid()); assertNull(d.value); assertFalse(d.old.valid()); assertEquals(1,events[0]);
    }
    @Test public void missingKeyFailsClosedAfterRestore() {
        Disk d=new Disk(); Crypto c=new Crypto(); new TokenVault(d,c,()->{});
        c.destroy(); TokenVault v=new TokenVault(d,c,()->{});
        assertFalse(v.read().valid()); assertNull(d.value);
    }
    @Test public void rotationIsOneBlobAndLogoutAllowsFreshLogin() {
        Disk d=new Disk(); Crypto c=new Crypto(); TokenVault v=new TokenVault(d,c,()->{});
        String oldBlob=d.value;
        assertTrue(v.save("rotated-access", "rotated-refresh"));
        assertNotEquals(oldBlob,d.value);
        assertEquals("rotated-refresh",v.read().refresh);
        v.clear(); assertFalse(v.read().valid()); assertNull(d.value); assertNull(c.key);
        assertTrue(v.save("new-access", "new-refresh")); assertTrue(v.read().valid());
    }
    @Test public void failedRotationNeverExposesMixedPair() {
        Disk d=new Disk(); Crypto c=new Crypto(); TokenVault v=new TokenVault(d,c,()->{});
        d.failWrite=true;
        assertFalse(v.save("new-access", "new-refresh")); assertFalse(v.read().valid()); assertNull(c.key);
    }
}
