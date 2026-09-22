package com.restaurant.viettable.ui.common;

import android.content.Context;
import android.content.DialogInterface;
import android.graphics.Color;
import android.graphics.Typeface;
import android.widget.Button;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.core.content.ContextCompat;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.restaurant.viettable.R;

public class DialogHelper {

    /**
     * Post-show button styler to guarantee high-contrast and readable button text.
     * Prevents text color from matching dialog or button background.
     */
    public static void styleDialog(@NonNull AlertDialog dialog, boolean isDestructive) {
        Context context = dialog.getContext();
        int positiveColor = isDestructive
                ? ContextCompat.getColor(context, R.color.semantic_error)
                : ContextCompat.getColor(context, R.color.primary);
        int secondaryColor = ContextCompat.getColor(context, R.color.text_secondary);

        Button positiveBtn = dialog.getButton(DialogInterface.BUTTON_POSITIVE);
        if (positiveBtn != null) {
            positiveBtn.setTextColor(positiveColor);
            positiveBtn.setBackgroundColor(Color.TRANSPARENT);
            positiveBtn.setTypeface(Typeface.DEFAULT_BOLD);
        }

        Button negativeBtn = dialog.getButton(DialogInterface.BUTTON_NEGATIVE);
        if (negativeBtn != null) {
            negativeBtn.setTextColor(secondaryColor);
            negativeBtn.setBackgroundColor(Color.TRANSPARENT);
            negativeBtn.setTypeface(Typeface.DEFAULT);
        }

        Button neutralBtn = dialog.getButton(DialogInterface.BUTTON_NEUTRAL);
        if (neutralBtn != null) {
            neutralBtn.setTextColor(secondaryColor);
            neutralBtn.setBackgroundColor(Color.TRANSPARENT);
            neutralBtn.setTypeface(Typeface.DEFAULT);
        }
    }

    /**
     * Builds and displays a standard Confirmation Dialog with Deep Green positive text and Neutral negative text.
     */
    public static AlertDialog showConfirmDialog(
            @NonNull Context context,
            @Nullable String title,
            @Nullable String message,
            @NonNull String positiveText,
            @Nullable DialogInterface.OnClickListener onPositive,
            @Nullable String negativeText,
            @Nullable DialogInterface.OnClickListener onNegative
    ) {
        MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(context);
        if (title != null) builder.setTitle(title);
        if (message != null) builder.setMessage(message);
        builder.setPositiveButton(positiveText, onPositive);
        if (negativeText != null) {
            builder.setNegativeButton(negativeText, onNegative);
        }

        AlertDialog dialog = builder.create();
        dialog.setOnShowListener(d -> styleDialog((AlertDialog) d, false));
        dialog.show();
        styleDialog(dialog, false);
        return dialog;
    }

    /**
     * Builds and displays a Destructive Dialog (e.g. Cancel Reservation, Logout, Clear Cart, Remove Item).
     * Positive button has high-contrast Error Red text.
     */
    public static AlertDialog showDestructiveDialog(
            @NonNull Context context,
            @Nullable String title,
            @Nullable String message,
            @NonNull String destructiveText,
            @Nullable DialogInterface.OnClickListener onDestructive,
            @Nullable String negativeText,
            @Nullable DialogInterface.OnClickListener onNegative
    ) {
        MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(context);
        if (title != null) builder.setTitle(title);
        if (message != null) builder.setMessage(message);
        builder.setPositiveButton(destructiveText, onDestructive);
        if (negativeText != null) {
            builder.setNegativeButton(negativeText, onNegative);
        }

        AlertDialog dialog = builder.create();
        dialog.setOnShowListener(d -> styleDialog((AlertDialog) d, true));
        dialog.show();
        styleDialog(dialog, true);
        return dialog;
    }

    /**
     * Builds and displays an Informational / Single-action Dialog.
     */
    public static AlertDialog showInfoDialog(
            @NonNull Context context,
            @Nullable String title,
            @Nullable String message,
            @NonNull String buttonText,
            @Nullable DialogInterface.OnClickListener onDismiss
    ) {
        MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(context);
        if (title != null) builder.setTitle(title);
        if (message != null) builder.setMessage(message);
        builder.setPositiveButton(buttonText, onDismiss);

        AlertDialog dialog = builder.create();
        dialog.setOnShowListener(d -> styleDialog((AlertDialog) d, false));
        dialog.show();
        styleDialog(dialog, false);
        return dialog;
    }
}
