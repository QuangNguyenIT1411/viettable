package com.restaurant.viettable.ui.common;

import android.content.Context;
import android.graphics.drawable.Drawable;
import android.widget.TextView;

import androidx.annotation.ColorRes;
import androidx.annotation.DrawableRes;
import androidx.core.content.ContextCompat;

import com.restaurant.viettable.R;
import com.restaurant.viettable.model.Reservation;

/**
 * Status pill (DESIGN_PHO_DEM.md section 5 "Nhãn trạng thái"), same look as the tracking screen (P04): coloured
 * fill, text and a 14dp icon in the text colour. The pill keeps its own text so the state is never colour-only.
 */
public final class PdStatusPill {

    public enum Tone { SUCCESS, WAITING, ERROR, NEUTRAL }

    private PdStatusPill() {}

    /** Booking states: confirmed / checked in = mint, waiting = yellow, cancelled / rejected / no-show = red. */
    public static Tone toneFor(Reservation.Status status) {
        if (status == null) return Tone.NEUTRAL;
        switch (status) {
            case CONFIRMED:
            case CHECKING_IN:
            case CHECKED_IN:
                return Tone.SUCCESS;
            case PENDING:
            case PENDING_DEPOSIT:
                return Tone.WAITING;
            case CANCELLED:
            case NO_SHOW:
            case REJECTED:
                return Tone.ERROR;
            default:
                return Tone.NEUTRAL;
        }
    }

    public static void apply(TextView pill, String text, Tone tone) {
        @ColorRes int fill, ink;
        @DrawableRes int icon;
        switch (tone) {
            case SUCCESS: fill = R.color.pd_mint; ink = R.color.pd_on_color; icon = R.drawable.ic_check; break;
            case WAITING: fill = R.color.pd_yellow; ink = R.color.pd_on_color; icon = R.drawable.ic_clock; break;
            case ERROR: fill = R.color.pd_red; ink = R.color.pd_on_color; icon = R.drawable.ic_close; break;
            default: fill = R.color.pd_surface_2; ink = R.color.pd_muted; icon = R.drawable.ic_info; break;
        }
        Context context = pill.getContext();
        pill.setText(text);
        pill.setBackgroundResource(R.drawable.bg_status_chip);
        pill.setBackgroundTintList(ContextCompat.getColorStateList(context, fill));
        pill.setTextColor(ContextCompat.getColor(context, ink));
        Drawable drawable = ContextCompat.getDrawable(context, icon);
        if (drawable != null) {
            drawable = drawable.mutate();
            int size = Math.round(14 * context.getResources().getDisplayMetrics().density);
            drawable.setBounds(0, 0, size, size);
        }
        pill.setCompoundDrawablesRelative(drawable, null, null, null);
        pill.setCompoundDrawableTintList(ContextCompat.getColorStateList(context, ink));
    }
}
