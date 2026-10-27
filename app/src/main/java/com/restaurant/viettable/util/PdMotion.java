package com.restaurant.viettable.util;

import android.content.Context;
import android.provider.Settings;
import android.view.View;
import android.view.ViewGroup;
import android.view.animation.AnimationUtils;
import android.view.animation.DecelerateInterpolator;
import android.view.animation.LayoutAnimationController;
import android.view.animation.OvershootInterpolator;

import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;

/** "Phố đêm" motion (DESIGN_PHO_DEM.md section 7). Everything is skipped when Android's "Remove animations" is on. */
public final class PdMotion {

    private PdMotion() {}

    public static boolean enabled(Context context) {
        return Settings.Global.getFloat(context.getContentResolver(),
                Settings.Global.ANIMATOR_DURATION_SCALE, 1f) > 0f;
    }

    /** Screen entry: each visible block slides up 24dp and fades in, 50ms apart, at most {@code maxBlocks}. */
    public static void enterBlocks(ViewGroup column, int maxBlocks) {
        if (!enabled(column.getContext())) return;
        float shift = 24 * column.getResources().getDisplayMetrics().density;
        DecelerateInterpolator decelerate = new DecelerateInterpolator(1.6f);
        int animated = 0;
        for (int i = 0; i < column.getChildCount() && animated < maxBlocks; i++) {
            View block = column.getChildAt(i);
            if (block.getVisibility() != View.VISIBLE) continue;
            block.setAlpha(0f);
            block.setTranslationY(shift);
            block.animate().alpha(1f).translationY(0f).setStartDelay(animated * 50L)
                    .setDuration(320).setInterpolator(decelerate).start();
            animated++;
        }
    }

    /** Big dish on the detail screen: turns from -30 degrees and grows 0.8 to 1 (400ms, slight overshoot); the price sticker pops in after it. */
    public static void dishEnter(View plate, View sticker) {
        if (!enabled(plate.getContext())) return;
        plate.setRotation(-30f);
        plate.setScaleX(0.8f);
        plate.setScaleY(0.8f);
        plate.setAlpha(0f);
        plate.animate().rotation(0f).scaleX(1f).scaleY(1f).alpha(1f).setDuration(400)
                .setInterpolator(new OvershootInterpolator(1.1f)).start();
        sticker.setScaleX(0f);
        sticker.setScaleY(0f);
        sticker.animate().scaleX(1f).scaleY(1f).setStartDelay(260).setDuration(320)
                .setInterpolator(new OvershootInterpolator(2f)).start();
    }

    /** Dish cards slide up one after another (stagger 50ms) the first time a list is laid out. */
    public static void staggerCards(RecyclerView list) {
        if (!enabled(list.getContext())) return;
        LayoutAnimationController controller = new LayoutAnimationController(
                AnimationUtils.loadAnimation(list.getContext(), R.anim.pd_card_enter), 0.15f);
        list.setLayoutAnimation(controller);
    }
}
