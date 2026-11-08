package com.restaurant.viettable.assistant;

import static org.junit.Assert.assertFalse;

import org.junit.Test;

import java.lang.reflect.Field;
import java.lang.reflect.Modifier;
import java.util.regex.Pattern;

/**
 * Owner 08/11: "Trợ lý đặt bàn" crashed on Android 7. Named groups "(?<h>…)" and Matcher.group(String) exist on
 * Android only from API 26 (the JVM running these tests has them, so the parser tests passed). minSdk is 24:
 * no pattern of the parser may use a named group.
 */
public class BookingIntentParserApi24Test {
    @Test
    public void noNamedGroupsBecauseMinSdkIs24() throws Exception {
        for (Field f : BookingIntentParser.class.getDeclaredFields()) {
            if (f.getType() != Pattern.class || !Modifier.isStatic(f.getModifiers())) continue;
            f.setAccessible(true);
            String regex = ((Pattern) f.get(null)).pattern();
            assertFalse(f.getName() + " uses a named group (API 26+): " + regex, regex.matches("(?s).*\\(\\?<[A-Za-z].*"));
        }
    }
}
