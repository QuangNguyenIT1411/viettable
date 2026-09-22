package com.restaurant.viettable.network;

import java.io.*;

/** Storage protocol independent of Android, so migration/failure ordering can be tested. */
public final class TokenVault {
    interface Persistence {
        String blob();
        boolean migrated();
        boolean write(String blob, boolean migrated);
        Pair legacy();
        boolean clearLegacy();
    }
    interface Encryption {
        String encrypt(byte[] value) throws Exception;
        byte[] decrypt(String value) throws Exception;
        void destroy() throws Exception;
    }
    static final class Pair {
        final String access, refresh;
        Pair(String access, String refresh) { this.access = access; this.refresh = refresh; }
        boolean valid() { return access != null && !access.isEmpty() && refresh != null && !refresh.isEmpty(); }
    }
    private final Persistence disk;
    private final Encryption encryption;
    private final Runnable invalidated;
    private boolean available = true;

    TokenVault(Persistence disk, Encryption encryption, Runnable invalidated) {
        this.disk = disk;
        this.encryption = encryption;
        this.invalidated = invalidated;
        boolean migrating = false;
        boolean verified = false;
        try {
            if (disk.blob() != null) {
                decode(encryption.decrypt(disk.blob()));
                if (!disk.clearLegacy()) throw new IOException();
            } else if (disk.migrated()) {
                if (!disk.clearLegacy()) throw new IOException();
            } else {
                Pair old = disk.legacy();
                if (old.valid()) {
                    migrating = true;
                    // Verify committed ciphertext before deleting the only recoverable plaintext copy.
                    persist(old);
                    verified = true;
                    if (!disk.clearLegacy()) throw new IOException();
                } else {
                    if (!disk.write(null, true) || !disk.clearLegacy()) throw new IOException();
                }
            }
        } catch (Exception failure) {
            // A failed initial write must not destroy the legacy copy. Never use it in this process.
            if (migrating && !verified) {
                try { disk.write(null, false); } catch (RuntimeException ignored) { }
                available = false;
            } else {
                clear();
                invalidated.run();
            }
        }
    }

    synchronized Pair read() {
        if (!available) return new Pair("", "");
        try {
            String blob = disk.blob();
            return blob == null ? new Pair("", "") : decode(encryption.decrypt(blob));
        } catch (Exception failure) {
            clear();
            invalidated.run();
            return new Pair("", "");
        }
    }

    synchronized boolean save(String access, String refresh) {
        try {
            Pair pair = new Pair(access, refresh);
            if (!pair.valid()) throw new IOException();
            persist(pair);
            if (!disk.clearLegacy()) throw new IOException();
            available = true;
            return true;
        } catch (Exception failure) {
            clear();
            invalidated.run();
            return false;
        }
    }

    private void persist(Pair pair) throws Exception {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        try (DataOutputStream out = new DataOutputStream(bytes)) {
            out.writeUTF(pair.access);
            out.writeUTF(pair.refresh);
        }
        String encrypted = encryption.encrypt(bytes.toByteArray());
        if (!disk.write(encrypted, true)) throw new IOException();
        Pair check = decode(encryption.decrypt(disk.blob()));
        if (!pair.access.equals(check.access) || !pair.refresh.equals(check.refresh)) throw new IOException();
    }

    synchronized void clear() {
        available = false;
        // Destroy the key too: failed disk cleanup cannot resurrect old tokens next launch.
        try { encryption.destroy(); } catch (Exception ignored) { }
        try { disk.write(null, true); } catch (RuntimeException ignored) { }
        try { disk.clearLegacy(); } catch (RuntimeException ignored) { }
    }

    private static Pair decode(byte[] value) throws IOException {
        try (DataInputStream in = new DataInputStream(new ByteArrayInputStream(value))) {
            Pair pair = new Pair(in.readUTF(), in.readUTF());
            if (!pair.valid() || in.available() != 0) throw new IOException();
            return pair;
        }
    }
}
