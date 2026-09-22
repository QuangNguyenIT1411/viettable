package com.restaurant.viettable.network;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.NetworkSecurityPolicy;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import org.junit.Test;
import org.junit.runner.RunWith;
import java.security.KeyStore;
import java.util.UUID;
import static org.junit.Assert.*;
import com.restaurant.viettable.testing.SmokeTest;

/** Run using adb am instrument after install -r of BOTH APKs; do not use connectedAndroidTest/UTP. */
@RunWith(AndroidJUnit4.class)
@SmokeTest
public class SecureTokenStoreDeviceTest {
    @Test public void realKeystoreMigrationRotationCorruptionAndLogout() throws Exception {
        Context ctx=InstrumentationRegistry.getInstrumentation().getTargetContext();
        String id="security_test_"+UUID.randomUUID();
        SharedPreferences legacy=ctx.getSharedPreferences(id+"_legacy",0);
        SharedPreferences disk=ctx.getSharedPreferences(id+"_secure",0);
        String alias=id+".v1";
        int[] invalidations={0};
        try {
            assertTrue(legacy.edit().putString("access_token","test-access").putString("refresh_token","test-refresh").commit());
            SecureTokenStore s=new SecureTokenStore(ctx,id+"_secure",id+"_legacy",alias,()->invalidations[0]++);
            assertEquals("test-access",s.getAccessToken()); assertEquals("test-refresh",s.getRefreshToken());
            assertFalse(legacy.contains("access_token")); assertFalse(legacy.contains("refresh_token"));
            assertTrue(disk.getString("auth_blob","").startsWith("v1:"));
            KeyStore ks=KeyStore.getInstance("AndroidKeyStore"); ks.load(null);
            assertNull(ks.getKey(alias,null).getEncoded());
            String first=disk.getString("auth_blob","");
            assertTrue(s.save("test-access","test-refresh"));
            assertNotEquals(first.split(":")[1],disk.getString("auth_blob","").split(":")[1]);
            SecureTokenStore reopened=new SecureTokenStore(ctx,id+"_secure",id+"_legacy",alias,()->invalidations[0]++);
            assertTrue(reopened.hasSession());
            disk.edit().putString("auth_blob","v1:!!!:broken").commit();
            assertFalse(reopened.hasSession()); assertEquals(1,invalidations[0]); assertFalse(disk.contains("auth_blob"));
            assertTrue(reopened.save("login-access","login-refresh"));
            ks.deleteEntry(alias);
            assertFalse(reopened.hasSession());
            assertTrue(reopened.save("after-key-loss","fresh-refresh"));
            reopened.clear(); assertFalse(reopened.hasSession()); assertFalse(disk.contains("auth_blob"));
            assertFalse(ks.containsAlias(alias));
        } finally {
            legacy.edit().clear().commit(); disk.edit().clear().commit();
            KeyStore ks=KeyStore.getInstance("AndroidKeyStore"); ks.load(null); ks.deleteEntry(alias);
        }
    }
    @Test public void debugNetworkOnlyAllowsNamedLocalHttp() {
        NetworkSecurityPolicy policy=NetworkSecurityPolicy.getInstance();
        assertTrue(policy.isCleartextTrafficPermitted("127.0.0.1"));
        assertTrue(policy.isCleartextTrafficPermitted("10.0.2.2"));
        assertFalse(policy.isCleartextTrafficPermitted("untrusted.invalid"));
    }
}
