package com.restaurant.viettable.util;

/**
 * Which branch the app shows. A branch is only ever one the user picked: per account on this phone (guests share
 * one choice), never the first branch of the list, never carried over from the old per-phone value (owner, 19/10).
 */
public final class BranchSelectionPolicy {
    private BranchSelectionPolicy() { }

    /** Old per-phone key; ignored and removed because nobody knows which account chose it. */
    public static final String LEGACY_KEY = "selectedBranchId";

    public static String prefKey(String accountId) {
        return BiometricLoginPolicy.isKnownAccount(accountId) ? "selectedBranchId_acc_" + accountId.trim()
                : "selectedBranchId_guest";
    }

    /** The saved branch if it still exists; otherwise none (Trang chủ shows "Chọn chi nhánh"). */
    public static String restore(String savedId, java.util.List<String> availableIds) {
        if (savedId == null) return null;
        return availableIds.contains(savedId) ? savedId : null;
    }

    /** Right after login an account that never chose a branch on this phone must choose one (no way back). */
    public static boolean mustChooseBranch(boolean afterLogin, boolean hasSavedChoice) {
        return afterLogin && !hasSavedChoice;
    }
}
