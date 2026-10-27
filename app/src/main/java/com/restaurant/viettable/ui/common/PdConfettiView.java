package com.restaurant.viettable.ui.common;

import android.animation.ValueAnimator;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.util.AttributeSet;
import android.view.View;
import android.view.animation.LinearInterpolator;

import androidx.annotation.Nullable;
import androidx.core.content.ContextCompat;

import com.restaurant.viettable.R;

import java.util.Random;

/** Coloured paper falling once (about 1.8s) over the success screen; draws nothing when idle or when animations are off. */
public class PdConfettiView extends View {

    private static final int COUNT = 30;
    private static final long RUN_MS = 1800;

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();
    private final float[] x = new float[COUNT];      // 0..1 of the width
    private final float[] startY = new float[COUNT]; // 0..1 of the height
    private final float[] fall = new float[COUNT];   // 0..1 of the height
    private final float[] delay = new float[COUNT];  // 0..0.35 of the run
    private final float[] spin = new float[COUNT];
    private final float[] size = new float[COUNT];   // dp
    private final boolean[] round = new boolean[COUNT];
    private final int[] colors;
    private float progress = 1f; // 1 = finished, nothing drawn
    private ValueAnimator running;

    public PdConfettiView(Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
        colors = new int[]{
                ContextCompat.getColor(context, R.color.pd_yellow),
                ContextCompat.getColor(context, R.color.pd_red),
                ContextCompat.getColor(context, R.color.pd_mint),
                ContextCompat.getColor(context, R.color.pd_cream)};
        Random random = new Random(7);
        for (int i = 0; i < COUNT; i++) {
            x[i] = random.nextFloat();
            startY[i] = -0.05f + random.nextFloat() * 0.25f;
            fall[i] = 0.35f + random.nextFloat() * 0.4f;
            delay[i] = random.nextFloat() * 0.35f;
            spin[i] = (random.nextFloat() - 0.5f) * 720f;
            size[i] = 6 + random.nextInt(8);
            round[i] = random.nextInt(4) == 0;
        }
        setClickable(false);
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
    }

    /** Plays once. Does nothing when the system turned animations off. */
    public void play() {
        if (!com.restaurant.viettable.util.PdMotion.enabled(getContext())) return;
        if (running != null) running.cancel();
        running = ValueAnimator.ofFloat(0f, 1f);
        running.setDuration(RUN_MS);
        running.setInterpolator(new LinearInterpolator());
        running.addUpdateListener(a -> {
            progress = (float) a.getAnimatedValue();
            invalidate();
        });
        running.start();
    }

    @Override
    protected void onDraw(Canvas canvas) {
        if (progress >= 1f) return;
        float density = getResources().getDisplayMetrics().density;
        for (int i = 0; i < COUNT; i++) {
            float t = (progress - delay[i]) / (1f - 0.35f);
            if (t <= 0f) continue;
            t = Math.min(1f, t);
            float cx = x[i] * getWidth();
            float cy = (startY[i] + fall[i] * t) * getHeight();
            float alpha = t < 0.75f ? 1f : (1f - t) / 0.25f;
            paint.setColor(colors[i % colors.length]);
            paint.setAlpha((int) (255 * Math.max(0f, alpha)));
            float s = size[i] * density;
            if (round[i]) {
                canvas.drawCircle(cx, cy, s / 2.2f, paint);
            } else {
                canvas.save();
                canvas.rotate(spin[i] * t, cx, cy);
                rect.set(cx - s / 2f, cy - s / 4f, cx + s / 2f, cy + s / 4f);
                canvas.drawRoundRect(rect, 2 * density, 2 * density, paint);
                canvas.restore();
            }
        }
    }

    @Override
    protected void onDetachedFromWindow() {
        if (running != null) running.cancel();
        super.onDetachedFromWindow();
    }
}
