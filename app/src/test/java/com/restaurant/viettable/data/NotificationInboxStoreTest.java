package com.restaurant.viettable.data;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

import org.junit.Test;

/** Owner 07/11: the inbox kept on the phone is shown only to the account it was loaded for. */
public class NotificationInboxStoreTest {
    @Test
    public void onlyTheSameAccountGetsTheSavedInbox() {
        assertEquals("[1]", NotificationInboxStore.pick("29", "[1]", "29"));
        assertEquals("[1]", NotificationInboxStore.pick(" 29 ", "[1]", "29"));
        assertNull(NotificationInboxStore.pick("29", "[1]", "30"));
        assertNull(NotificationInboxStore.pick("29", "[1]", null));
        assertNull(NotificationInboxStore.pick(null, "[1]", "29"));
        assertNull(NotificationInboxStore.pick("29", null, "29"));
    }
}
