package com.restaurant.viettable.ui.common;

import android.view.View;
import android.widget.TextView;

import androidx.annotation.Nullable;

/**
 * One state for a loaded list: the rows, a spinner, an empty message, or an error message with "Thử lại". A failed
 * load replaces the list (the retry button used to sit on top of the rows still shown); a reload with rows keeps them.
 */
public final class ListState {
    private ListState() { }

    public enum Mode { CONTENT, LOADING, EMPTY, ERROR }

    public static Mode of(boolean hasContent, boolean loading, @Nullable String error) {
        if (loading) return hasContent ? Mode.CONTENT : Mode.LOADING;
        if (error != null) return Mode.ERROR;
        return hasContent ? Mode.CONTENT : Mode.EMPTY;
    }

    /** Shows exactly one of the views for {@link #of}; the message gets the error or the empty text. */
    public static Mode render(View content, @Nullable View progress, @Nullable TextView message, @Nullable View retry,
                              boolean hasContent, boolean loading, @Nullable String error, CharSequence emptyText) {
        Mode mode = of(hasContent, loading, error);
        content.setVisibility(mode == Mode.CONTENT ? View.VISIBLE : View.GONE);
        if (progress != null) progress.setVisibility(mode == Mode.LOADING ? View.VISIBLE : View.GONE);
        if (message != null) {
            boolean shown = mode == Mode.EMPTY || mode == Mode.ERROR;
            message.setVisibility(shown ? View.VISIBLE : View.GONE);
            if (shown) message.setText(mode == Mode.ERROR ? error : emptyText);
        }
        if (retry != null) retry.setVisibility(mode == Mode.ERROR ? View.VISIBLE : View.GONE);
        return mode;
    }
}
