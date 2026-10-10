package com.restaurant.viettable.data;

import com.restaurant.viettable.model.DiningSession;
import org.junit.Test;
import static org.junit.Assert.*;

public class TableNumberDirectoryTest {
    @Test public void displayUsesCatalogNumberNotInternalId() {
        TableNumberDirectory directory = new TableNumberDirectory();
        directory.put("branch-a", "internal-37", 2);
        assertEquals("Bàn 2", directory.label("branch-a", "internal-37"));
        DiningSession session = new DiningSession("session", "branch-a", "Branch", "internal-37",
                DiningSession.Status.ACTIVE, null, "", "", null, null);
        session.setTableLabel(directory.label(session.getBranchId(), session.getTableId()));
        assertEquals("internal-37", session.getTableId());
        assertEquals("Bàn 2", session.getTableLabel());
    }

    @Test public void identitiesAreBranchScopedAndUnknownDoesNotLeakId() {
        TableNumberDirectory directory = new TableNumberDirectory();
        directory.put("a", "same-id", 2);
        directory.put("b", "same-id", 9);
        assertEquals("Bàn 2", directory.label("a", "same-id"));
        assertEquals("Bàn 9", directory.label("b", "same-id"));
        assertEquals("Chưa có thông tin bàn", directory.label("c", "same-id"));
        assertEquals("Chưa xếp bàn", directory.label("a", ""));
        directory.clear();
        assertEquals("Chưa có thông tin bàn", directory.label("a", "same-id"));
    }
}
