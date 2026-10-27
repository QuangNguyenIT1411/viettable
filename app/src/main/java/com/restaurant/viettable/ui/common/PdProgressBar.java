package com.restaurant.viettable.ui.common;

import android.animation.ValueAnimator;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.Shader;
import android.util.AttributeSet;
import android.view.View;
import android.view.animation.DecelerateInterpolator;

import androidx.annotation.Nullable;
import androidx.core.content.ContextCompat;

import com.restaurant.viettable.R;

/**
 * Progress bar of the Tiến độ món card (DESIGN_PHO_DEM.md section 6, P04): surface_2 track, red to yellow fill and a
 * cream round knob at the end of the fill. Runs to a new value in 600ms.
 */
public class PdProgressBar extends View {

    private static final long RUN_MS = 600;

    private final Paint track = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint fill = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint knob = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();
    private final float barHeight;
    private final float knobRadius;
    private float shown;      // 0..1 currently drawn
    private ValueAnimator running;

    public PdProgressBar(Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
        float density = getResources().getDisplayMetrics().density;
        barHeight = 14 * density;
        knobRadius = 12 * density;
        track.setColor(ContextCompat.getColor(context, R.color.pd_surface_2));
        knob.setColor(ContextCompat.getColor(context, R.color.pd_cream));
        setMinimumHeight((int) (knobRadius * 2));
    }

    /** Same shape as the Material indicator it replaces: percent 0..100, animated when asked. */
    public void setProgressCompat(int percent, boolean animated) {
        float next = Math.max(0f, Math.min(1f, percent / 100f));
        if (running != null) running.cancel();
        if (!animated || !isAttachedToWindow() || next == shown) {
            shown = next;
            invalidate();
            return;
        }
        running = ValueAnimator.ofFloat(shown, next);
        running.setDuration(RUN_MS);
        running.setInterpolator(new DecelerateInterpolator(1.4f));
        running.addUpdateListener(a -> {
            shown = (float) a.getAnimatedValue();
            invalidate();
        });
        running.start();
    }

    @Override
    protected void onMeasure(int widthSpec, int heightSpec) {
        int height = (int) Math.ceil(knobRadius * 2);
        setMeasuredDimension(resolveSize(getSuggestedMinimumWidth(), widthSpec), resolveSize(height, heightSpec));
    }

    @Override
    protected void onSizeChanged(int w, int h, int oldw, int oldh) {
        float left = knobRadius, right = Math.max(knobRadius, w - knobRadius);
        fill.setShader(new LinearGradient(left, 0, right, 0,
                ContextCompat.getColor(getContext(), R.color.pd_red),
                ContextCompat.getColor(getContext(), R.color.pd_yellow), Shader.TileMode.CLAMP));
    }

    @Override
    protected void onDraw(Canvas canvas) {
        float w = getWidth(), cy = getHeight() / 2f, half = barHeight / 2f;
        rect.set(0, cy - half, w, cy + half);
        canvas.drawRoundRect(rect, half, half, track);
        if (shown <= 0f) return;
        float knobX = knobRadius + (w - 2 * knobRadius) * shown;
        rect.set(0, cy - half, knobX, cy + half);
        canvas.drawRoundRect(rect, half, half, fill);
        canvas.drawCircle(knobX, cy, knobRadius, knob);
    }

    @Override
    protected void onDetachedFromWindow() {
        if (running != null) running.cancel();
        super.onDetachedFromWindow();
    }
}
