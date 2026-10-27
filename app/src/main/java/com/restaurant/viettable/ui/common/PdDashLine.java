package com.restaurant.viettable.ui.common;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.DashPathEffect;
import android.graphics.Paint;
import android.util.AttributeSet;
import android.view.View;

import androidx.annotation.Nullable;
import androidx.core.content.ContextCompat;

import com.restaurant.viettable.R;

/** Dotted tear line across the VietQR ticket (round dots, on_color), centred in its own height. */
public class PdDashLine extends View {

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);

    public PdDashLine(Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
        float density = getResources().getDisplayMetrics().density;
        paint.setColor(ContextCompat.getColor(context, R.color.pd_on_color));
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(4 * density);
        paint.setStrokeCap(Paint.Cap.ROUND);
        paint.setPathEffect(new DashPathEffect(new float[]{0.1f, 14 * density}, 0));
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
    }

    @Override
    protected void onDraw(Canvas canvas) {
        float y = getHeight() / 2f;
        canvas.drawLine(0, y, getWidth(), y, paint);
    }
}
