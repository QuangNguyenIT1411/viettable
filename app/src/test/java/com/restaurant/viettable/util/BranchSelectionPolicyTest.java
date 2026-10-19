package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotEquals;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import java.util.Arrays;

import org.junit.Test;

public class BranchSelectionPolicyTest {
    // Bug 19/10: with nothing chosen, Trang chủ showed the first branch of the list ("Chi nhánh Bình Thạnh").
    @Test public void nothing_chosen_means_no_branch_never_the_first_one() {
        assertNull(BranchSelectionPolicy.restore(null, Arrays.asList("3", "1", "2")));
    }

    @Test public void a_saved_branch_is_kept_while_it_still_exists() {
        assertEquals("1", BranchSelectionPolicy.restore("1", Arrays.asList("3", "1", "2")));
        assertNull(BranchSelectionPolicy.restore("9", Arrays.asList("3", "1", "2")));
    }

    @Test public void each_account_has_its_own_choice_and_guests_share_one() {
        assertNotEquals(BranchSelectionPolicy.prefKey("25"), BranchSelectionPolicy.prefKey("26"));
        assertEquals(BranchSelectionPolicy.prefKey(null), BranchSelectionPolicy.prefKey("0"));
        // The old per-phone value is never reused for anyone.
        assertNotEquals(BranchSelectionPolicy.LEGACY_KEY, BranchSelectionPolicy.prefKey("25"));
        assertNotEquals(BranchSelectionPolicy.LEGACY_KEY, BranchSelectionPolicy.prefKey(null));
    }

    @Test public void choosing_is_mandatory_only_right_after_login_without_a_saved_choice() {
        assertTrue(BranchSelectionPolicy.mustChooseBranch(true, false));
        assertFalse(BranchSelectionPolicy.mustChooseBranch(true, true));
        assertFalse(BranchSelectionPolicy.mustChooseBranch(false, false));
    }
}
