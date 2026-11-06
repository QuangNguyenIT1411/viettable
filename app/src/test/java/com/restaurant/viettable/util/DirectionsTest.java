package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class DirectionsTest {
    @Test
    public void onlyARealPositionCounts() {
        assertTrue(Directions.isValid(10.776889, 106.700806));
        assertFalse(Directions.isValid(0, 0));
        assertFalse(Directions.isValid(91, 106));
        assertFalse(Directions.isValid(10, 181));
        assertFalse(Directions.isValid(Double.NaN, 106));
    }

    @Test
    public void geoLinkCarriesThePointAndTheName() {
        assertEquals("geo:10.776889,106.700806?q=10.776889,106.700806(Chi%20nh%C3%A1nh%20Qu%E1%BA%ADn%201)",
                Directions.geoUri(10.776889, 106.700806, "Chi nhánh Quận 1"));
        assertEquals("geo:10.776889,106.700806?q=10.776889,106.700806", Directions.geoUri(10.776889, 106.700806, null));
    }

    @Test
    public void webFallbackIsAGoogleMapsRoute() {
        assertEquals("https://www.google.com/maps/dir/?api=1&destination=10.776889,106.700806",
                Directions.webUri(10.776889, 106.700806));
    }
}
