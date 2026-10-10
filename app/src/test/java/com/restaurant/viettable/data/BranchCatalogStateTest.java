package com.restaurant.viettable.data;

import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.Category;
import org.junit.After;
import org.junit.Test;
import java.util.Arrays;
import java.util.Collections;
import static org.junit.Assert.*;

public class BranchCatalogStateTest {
    private final AppStateManager state = AppStateManager.getInstance();

    private Branch branch(String id) {
        return new Branch(id, "Branch " + id, "", "", "", 0, true, "",
                Collections.emptyList(), 0, 0, 0, 0);
    }

    @After public void cleanup() {
        state.replaceBranches(Collections.emptyList());
        state.replaceCatalog(Collections.emptyList(), Collections.emptyList());
    }

    @Test public void branchChangeClearsPreviousCatalogWithoutChangingSession() {
        state.setSelectedBranch(branch("a"));
        state.replaceBranchCatalog("a", Collections.singletonList(new Category("c", "Category", 0, 0)), Collections.emptyList());
        Object session = state.getActiveDiningSession();
        state.setSelectedBranch(branch("b"));
        assertTrue(state.getCategories().isEmpty());
        assertNull(state.getCatalogBranchId());
        assertSame(session, state.getActiveDiningSession());
    }

    @Test public void staleResponseCannotOverwriteSelectedBranch() {
        state.setSelectedBranch(branch("a"));
        state.beginCatalogLoad("a");
        state.setSelectedBranch(branch("b"));
        assertFalse(state.replaceBranchCatalog("a", Collections.singletonList(new Category("c", "Old", 0, 0)), Collections.emptyList()));
        assertTrue(state.getCategories().isEmpty());
        state.failCatalogLoad("a", "old error");
        assertNull(state.getCatalogError());
    }

    @Test public void refreshedBranchListRetainsExplicitSelection() {
        state.setSelectedBranch(branch("b"));
        state.replaceBranches(Arrays.asList(branch("a"), branch("b")));
        assertEquals("b", state.getSelectedBranch().getId());
    }

    @Test public void retryClearsErrorAndSuccessfulEmptyResponseEndsLoading() {
        state.setSelectedBranch(branch("b"));
        state.beginCatalogLoad("b");
        state.failCatalogLoad("b", "network failure");
        assertFalse(state.isCatalogLoading());
        assertEquals("network failure", state.getCatalogError());
        state.beginCatalogLoad("b");
        assertTrue(state.isCatalogLoading());
        assertNull(state.getCatalogError());
        assertTrue(state.replaceBranchCatalog("b", Collections.emptyList(), Collections.emptyList()));
        assertFalse(state.isCatalogLoading());
        assertEquals("b", state.getCatalogBranchId());
    }
}
