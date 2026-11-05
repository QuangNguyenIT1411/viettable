package com.restaurant.viettable.design;

import static org.junit.Assume.assumeTrue;

import android.content.Context;
import android.content.res.ColorStateList;
import android.content.res.Configuration;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Rect;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.GradientDrawable;
import android.text.Layout;
import android.view.ContextThemeWrapper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.HorizontalScrollView;
import android.widget.ScrollView;
import android.widget.TextView;

import androidx.core.widget.NestedScrollView;
import androidx.recyclerview.widget.RecyclerView;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;
import com.google.android.material.shape.MaterialShapeDrawable;
import com.restaurant.viettable.R;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.io.File;
import java.io.FileOutputStream;
import java.io.PrintWriter;
import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.List;

/**
 * Manual design check (owner 05/11), not part of the regular suite: inflates every layout in Sáng and Tối at 100% and 130%
 * text on a 360 dp phone, writes a picture of each screen and lists clipped text, overlapping text, small touch targets,
 * views off the screen and low contrast. Offline. Run only with -e layoutAudit true.
 */
@RunWith(AndroidJUnit4.class)
public class LayoutAuditTest {
    private static final int DENSITY_DPI = 480;
    private static final int WIDTH = 1080;
    private static final int HEIGHT = 2400;

    private static final class TextBox {
        final View view; final Rect rect; final boolean scrolling;
        TextBox(View view, Rect rect, boolean scrolling) { this.view = view; this.rect = rect; this.scrolling = scrolling; }
    }

    @Test
    public void auditEveryLayout() throws Exception {
        assumeTrue("layout audit only", "true".equals(InstrumentationRegistry.getArguments().getString("layoutAudit")));
        Context target = InstrumentationRegistry.getInstrumentation().getTargetContext();
        File out = new File(target.getExternalFilesDir(null), "layout_audit");
        out.mkdirs();
        try (PrintWriter report = new PrintWriter(new File(out, "report.csv"), "UTF-8")) {
            report.println("layout,mode,font,kind,view,detail");
            for (Field field : R.layout.class.getFields()) {
                int id = field.getInt(null);
                String name = field.getName();
                if (name.startsWith("abc_") || name.startsWith("m3_") || name.startsWith("mtrl_") || name.startsWith("material_")
                        || name.startsWith("design_") || name.startsWith("notification_") || name.startsWith("select_dialog")
                        || name.startsWith("support_") || name.startsWith("custom_dialog") || name.startsWith("ime_") || name.startsWith("browser_")) continue;
                for (boolean night : new boolean[]{false, true}) {
                    for (float font : new float[]{1f, 1.3f}) {
                        InstrumentationRegistry.getInstrumentation().runOnMainSync(() -> audit(target, id, name, night, font, out, report));
                    }
                }
                report.flush();
            }
        }
    }

    private static void audit(Context base, int id, String name, boolean night, float font, File out, PrintWriter report) {
        String mode = night ? "toi" : "sang";
        String fontLabel = font > 1f ? "130" : "100";
        try {
            Configuration config = new Configuration(base.getResources().getConfiguration());
            config.uiMode = (config.uiMode & ~Configuration.UI_MODE_NIGHT_MASK) | (night ? Configuration.UI_MODE_NIGHT_YES : Configuration.UI_MODE_NIGHT_NO);
            config.fontScale = font;
            config.densityDpi = DENSITY_DPI;
            config.screenWidthDp = 360;
            config.screenHeightDp = 800;
            Context context = new ContextThemeWrapper(base.createConfigurationContext(config), R.style.Theme_VietTable);
            FrameLayout host = new FrameLayout(context);
            LayoutInflater.from(context).inflate(id, host, true);
            boolean screen = name.startsWith("fragment") || name.startsWith("activity");
            host.measure(View.MeasureSpec.makeMeasureSpec(WIDTH, View.MeasureSpec.EXACTLY),
                    screen ? View.MeasureSpec.makeMeasureSpec(HEIGHT, View.MeasureSpec.EXACTLY) : View.MeasureSpec.makeMeasureSpec(HEIGHT, View.MeasureSpec.AT_MOST));
            host.layout(0, 0, host.getMeasuredWidth(), host.getMeasuredHeight());
            float density = DENSITY_DPI / 160f;
            List<TextBox> boxes = new ArrayList<>();
            walk(host, 0, 0, false, false, density, boxes, line -> report.println(name + "," + mode + "," + fontLabel + "," + line));
            for (int i = 0; i < boxes.size(); i++) {
                for (int j = i + 1; j < boxes.size(); j++) {
                    TextBox a = boxes.get(i), b = boxes.get(j);
                    if (a.scrolling != b.scrolling) continue; // a fixed bar over scrolling content is by design
                    if (isAncestor(a.view, b.view) || isAncestor(b.view, a.view)) continue;
                    Rect overlap = new Rect();
                    if (overlap.setIntersect(a.rect, b.rect) && overlap.width() > 6 && overlap.height() > 6) {
                        report.println(name + "," + mode + "," + fontLabel + ",OVERLAP," + idName(a.view) + "," + idName(b.view) + " " + clean(textOf(a.view)) + " / " + clean(textOf(b.view)));
                    }
                }
            }
            if (host.getMeasuredWidth() > 0 && host.getMeasuredHeight() > 0) {
                Bitmap bitmap = Bitmap.createBitmap(host.getMeasuredWidth(), host.getMeasuredHeight(), Bitmap.Config.ARGB_8888);
                bitmap.eraseColor(context.getColor(R.color.pd_bg));
                host.draw(new Canvas(bitmap));
                Bitmap small = Bitmap.createScaledBitmap(bitmap, bitmap.getWidth() / 3, bitmap.getHeight() / 3, true);
                try (FileOutputStream stream = new FileOutputStream(new File(out, name + "__" + mode + "_" + fontLabel + ".png"))) {
                    small.compress(Bitmap.CompressFormat.PNG, 90, stream);
                }
                bitmap.recycle();
                small.recycle();
            }
        } catch (Throwable error) {
            report.println(name + "," + mode + "," + fontLabel + ",INFLATE_ERROR,," + clean(String.valueOf(error.getMessage())));
        }
    }

    private interface Sink { void add(String line); }

    private static void walk(View view, int parentX, int parentY, boolean scrolling, boolean horizontal, float density, List<TextBox> boxes, Sink sink) {
        if (view.getVisibility() != View.VISIBLE) return;
        int x = parentX + view.getLeft();
        int y = parentY + view.getTop();
        int w = view.getWidth();
        int h = view.getHeight();
        if (view.isClickable() && w > 0 && h > 0 && !(view instanceof EditText)) {
            int min = (int) (44 * density);
            if (w < min || h < min) sink.add("SMALL_TARGET," + idName(view) + "," + Math.round(w / density) + "x" + Math.round(h / density) + "dp");
        }
        if (!horizontal && w > 0 && x + w > WIDTH + 1 && !(view.getParent() instanceof FrameLayout && w > WIDTH / 2 && h > WIDTH / 2)) {
            sink.add("OFFSCREEN," + idName(view) + "," + (x + w - WIDTH) + "px");
        }
        if (view instanceof TextView) checkText((TextView) view, x, y, scrolling, density, boxes, sink);
        if (view instanceof ViewGroup) {
            boolean nextScrolling = scrolling || view instanceof ScrollView || view instanceof NestedScrollView || view instanceof RecyclerView;
            boolean nextHorizontal = horizontal || view instanceof HorizontalScrollView;
            ViewGroup group = (ViewGroup) view;
            for (int i = 0; i < group.getChildCount(); i++) walk(group.getChildAt(i), x, y, nextScrolling, nextHorizontal, density, boxes, sink);
        }
    }

    private static void checkText(TextView text, int x, int y, boolean scrolling, float density, List<TextBox> boxes, Sink sink) {
        CharSequence content = text.getText();
        if (content == null || content.length() == 0 || text.getWidth() == 0) return;
        Layout layout = text.getLayout();
        if (layout != null) {
            int room = text.getHeight() - text.getCompoundPaddingTop() - text.getCompoundPaddingBottom();
            if (layout.getHeight() > room + 2) sink.add("CLIP_HEIGHT," + idName(text) + "," + clean(content) + " need " + layout.getHeight() + " have " + room);
            for (int line = 0; line < layout.getLineCount(); line++) {
                if (layout.getEllipsisCount(line) > 0) { sink.add("ELLIPSIS," + idName(text) + "," + clean(content)); break; }
            }
            int roomWide = text.getWidth() - text.getCompoundPaddingLeft() - text.getCompoundPaddingRight();
            for (int line = 0; line < layout.getLineCount(); line++) {
                if (layout.getLineWidth(line) > roomWide + 2) { sink.add("CLIP_WIDTH," + idName(text) + "," + clean(content)); break; }
            }
        }
        boxes.add(new TextBox(text, new Rect(x, y, x + text.getWidth(), y + text.getHeight()), scrolling));
        if (!(text instanceof EditText)) {
            int background = backgroundOf(text);
            if (background != 0) {
                int color = text.getCurrentTextColor();
                if (Color.alpha(color) == 255) {
                    double ratio = contrast(color, background);
                    boolean large = text.getTextSize() / density >= 18 * text.getResources().getConfiguration().fontScale;
                    if (ratio < (large ? 3 : 4.5)) sink.add("CONTRAST," + idName(text) + "," + String.format(java.util.Locale.ROOT, "%.2f", ratio) + " " + clean(content));
                }
            }
        }
    }

    /** The first opaque colour behind the view; 0 when it cannot be told (an image, a gradient). */
    private static int backgroundOf(View start) {
        View view = start;
        while (view != null) {
            int color = 0;
            if (view instanceof MaterialCardView) {
                ColorStateList list = ((MaterialCardView) view).getCardBackgroundColor();
                color = list == null ? 0 : list.getDefaultColor();
            } else if (view instanceof MaterialButton && ((MaterialButton) view).getBackgroundTintList() != null) {
                color = ((MaterialButton) view).getBackgroundTintList().getColorForState(view.getDrawableState(), 0);
            } else {
                Drawable drawable = view.getBackground();
                if (drawable instanceof ColorDrawable) color = ((ColorDrawable) drawable).getColor();
                else if (drawable instanceof GradientDrawable) {
                    GradientDrawable gradient = (GradientDrawable) drawable;
                    if (gradient.getColors() != null) return 0;
                    ColorStateList list = gradient.getColor();
                    if (list != null) color = list.getDefaultColor();
                } else if (drawable instanceof MaterialShapeDrawable) {
                    ColorStateList list = ((MaterialShapeDrawable) drawable).getFillColor();
                    if (list != null) color = list.getDefaultColor();
                } else if (drawable != null && view != start) {
                    return 0;
                }
            }
            if (Color.alpha(color) == 255) return color;
            if (Color.alpha(color) > 0) return 0;
            view = view.getParent() instanceof View ? (View) view.getParent() : null;
        }
        return start.getContext().getColor(R.color.pd_bg);
    }

    private static double contrast(int a, int b) {
        double la = luminance(a), lb = luminance(b);
        return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
    }

    private static double luminance(int color) {
        return 0.2126 * channel(Color.red(color)) + 0.7152 * channel(Color.green(color)) + 0.0722 * channel(Color.blue(color));
    }

    private static double channel(int value) {
        double v = value / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
    }

    private static boolean isAncestor(View ancestor, View view) {
        Object parent = view.getParent();
        while (parent instanceof View) {
            if (parent == ancestor) return true;
            parent = ((View) parent).getParent();
        }
        return false;
    }

    private static String textOf(View view) { return view instanceof TextView ? String.valueOf(((TextView) view).getText()) : ""; }

    private static String idName(View view) {
        if (view.getId() == View.NO_ID) return view.getClass().getSimpleName();
        try { return view.getResources().getResourceEntryName(view.getId()); } catch (Exception e) { return String.valueOf(view.getId()); }
    }

    private static String clean(CharSequence value) {
        String text = String.valueOf(value).replace(',', ' ').replace('\n', ' ');
        return text.length() > 50 ? text.substring(0, 50) : text;
    }
}
