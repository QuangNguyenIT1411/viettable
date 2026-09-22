package com.restaurant.viettable;

import com.restaurant.viettable.util.QrPayloadParser;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

public class QrPayloadParserTest {
    @Test
    public void parsesRawQrCodeWithoutInventingASchema() {
        assertEquals("TABLE-QR-CODE", QrPayloadParser.parse("  TABLE-QR-CODE  "));
    }

    @Test
    public void rejectsMissingOrOversizedPayload() {
        assertNull(QrPayloadParser.parse(null));
        assertNull(QrPayloadParser.parse("   "));
        assertNull(QrPayloadParser.parse(new String(new char[101]).replace('\0', 'A')));
    }

    @Test
    public void rejectsControlCharacters() {
        assertNull(QrPayloadParser.parse("TABLE\nQR"));
    }
}
