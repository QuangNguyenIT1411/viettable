package com.restaurant.viettable.ui.common;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.util.AttributeSet;
import android.view.View;

import androidx.annotation.Nullable;
import androidx.core.content.ContextCompat;

import com.restaurant.viettable.R;

/**
 * Booking wizard progress "1/5 … 5/5" as pill segments (DESIGN_PHO_DEM.md section 6 "Đặt bàn"): done = yellow,
 * current = cream, not yet = surface_2. Takes the same 0–100 progress the old progress bar did.
 */
public class PdStepSegments extends View {

    private static final int STEPS = 5;

    private final Paint done = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint current = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint todo = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();
    private final float gap;
    private int progress;

    public PdStepSegments(Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
        gap = 6 * getResources().getDisplayMetrics().density;
        done.setColor(ContextCompat.getColor(context, R.color.pd_yellow));
        current.setColor(ContextCompat.getColor(context, R.color.pd_cream));
        todo.setColor(ContextCompat.getColor(context, R.color.pd_surface_2));
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
    }

    /** 20 = step 1, 40 = step 2 … 100 = step 5. */
    public void setProgress(int progress) {
        this.progress = Math.max(0, Math.min(100, progress));
        invalidate();
    }

    @Override
    protected void onDraw(Canvas canvas) {
        int step = Math.max(1, Math.round(progress / (100f / STEPS)));
        float width = (getWidth() - gap * (STEPS - 1)) / STEPS;
        float radius = getHeight() / 2f;
        for (int i = 0; i < STEPS; i++) {
            float left = i * (width + gap);
            rect.set(left, 0, left + width, getHeight());
            canvas.drawRoundRect(rect, radius, radius, i + 1 < step ? done : i + 1 == step ? current : todo);
        }
    }
}
