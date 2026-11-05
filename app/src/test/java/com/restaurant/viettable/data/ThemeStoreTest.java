package com.restaurant.viettable.data;

import static org.junit.Assert.assertEquals;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatDelegate;

import com.restaurant.viettable.util.ThemeChoice;

import org.junit.Test;

public class ThemeStoreTest {
    private static final class Memory implements ThemeStore.Backing {
        String value;
        @Nullable @Override public String read() { return value; }
        @Override public void write(String key) { value = key; }
    }

    @Test
    public void nothingSavedMeansTheDarkDemoLook() {
        assertEquals(ThemeChoice.DARK, new ThemeStore(new Memory()).get());
        assertEquals(AppCompatDelegate.MODE_NIGHT_YES, ThemeChoice.DEFAULT.nightMode);
    }

    @Test
    public void everyChoiceIsRememberedAndReadBackAfterARestart() {
        Memory memory = new Memory();
        ThemeStore store = new ThemeStore(memory);
        for (ThemeChoice choice : ThemeChoice.values()) {
            store.save(choice);
            assertEquals(choice, store.get());
            assertEquals(choice, new ThemeStore(memory).get());
        }
    }

    @Test
    public void unknownOrBlankKeysFallBackToDark() {
        assertEquals(ThemeChoice.DARK, ThemeChoice.fromKey(null));
        assertEquals(ThemeChoice.DARK, ThemeChoice.fromKey(""));
        assertEquals(ThemeChoice.DARK, ThemeChoice.fromKey("neon"));
        assertEquals(ThemeChoice.LIGHT, ThemeChoice.fromKey(" light "));
    }

    @Test
    public void choicesMapToTheRightNightModeAndLabel() {
        assertEquals(AppCompatDelegate.MODE_NIGHT_FOLLOW_SYSTEM, ThemeChoice.SYSTEM.nightMode);
        assertEquals(AppCompatDelegate.MODE_NIGHT_NO, ThemeChoice.LIGHT.nightMode);
        assertEquals(AppCompatDelegate.MODE_NIGHT_YES, ThemeChoice.DARK.nightMode);
        assertEquals("Theo hệ thống", ThemeChoice.SYSTEM.label);
        assertEquals(3, ThemeChoice.labels().length);
    }
}
