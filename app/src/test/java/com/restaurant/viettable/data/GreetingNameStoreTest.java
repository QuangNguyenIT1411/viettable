package com.restaurant.viettable.data;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class GreetingNameStoreTest {
    @Test
    public void savedNameOnlyForTheSameAccount() {
        assertEquals("Vinh", GreetingNameStore.pick("42", "Vinh", "42"));
        assertEquals("Vinh", GreetingNameStore.pick("42", "Vinh", " 42 "));
        assertEquals("", GreetingNameStore.pick("42", "Vinh", "43"));
        assertEquals("", GreetingNameStore.pick("42", "Vinh", null));
        assertEquals("", GreetingNameStore.pick(null, null, "42"));
    }
}
