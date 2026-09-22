package com.restaurant.viettable;

import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;

import org.junit.After;
import org.junit.Before;
import org.junit.Test;

import java.util.ArrayList;
import java.util.Arrays;

import static org.junit.Assert.*;

public class DiningSessionPersistenceTest {

    private AppStateManager appStateManager;

    @Before
    public void setUp() {
        appStateManager = AppStateManager.getInstance();
        appStateManager.setActiveDiningSession(null);
    }

    @After
    public void tearDown() {
        appStateManager.setActiveDiningSession(null);
    }

    @Test
    public void activeDiningSessionStateComesFromExplicitStateUpdate() {
        appStateManager.setActiveDiningSession(createTestSession());
        DiningSession session = appStateManager.getActiveDiningSession();

        assertNotNull("Dining session must exist after a repository-style state update", session);
        assertEquals(DiningSession.Status.ACTIVE, session.getStatus());
        assertEquals("Bàn B12", session.getTableId());
        assertTrue("Session must have orders", session.getOrders().size() >= 2);

        // Verify orders state
        Order order1 = session.getOrders().get(0);
        Order order2 = session.getOrders().get(1);
        assertEquals(Order.Status.COMPLETED, order1.getStatus());
        assertEquals(Order.Status.IN_PROGRESS, order2.getStatus());
    }

    @Test
    public void paymentRequestedStatePersistsInMemory() {
        appStateManager.setActiveDiningSession(createTestSession());
        DiningSession session = appStateManager.getActiveDiningSession();
        assertNotNull(session);

        // User requests payment
        session.setStatus(DiningSession.Status.PAYMENT_REQUESTED);
        assertEquals(DiningSession.Status.PAYMENT_REQUESTED, session.getStatus());

        // Simulate returning to home and re-accessing active session
        DiningSession reloadedSession = appStateManager.getActiveDiningSession();
        assertSame(session, reloadedSession);
        assertEquals(DiningSession.Status.PAYMENT_REQUESTED, reloadedSession.getStatus());
        assertEquals(3, reloadedSession.getOrders().size());
        assertEquals(Order.Status.COMPLETED, reloadedSession.getOrders().get(0).getStatus());
        assertEquals(Order.Status.IN_PROGRESS, reloadedSession.getOrders().get(1).getStatus());
        assertEquals(Order.Status.PLACED, reloadedSession.getOrders().get(2).getStatus());
    }

    @Test
    public void readingSessionDoesNotClearIt() {
        appStateManager.setActiveDiningSession(createTestSession());
        DiningSession session = appStateManager.getActiveDiningSession();
        assertNotNull(session);
        assertEquals(DiningSession.Status.ACTIVE, session.getStatus());

        DiningSession persistedSession = appStateManager.getActiveDiningSession();
        assertNotNull("Session must not be null after navigating home", persistedSession);
        assertEquals(DiningSession.Status.ACTIVE, persistedSession.getStatus());
        assertEquals("VietTable Hoàn Kiếm", persistedSession.getBranchName());
        assertEquals("Bàn B12", persistedSession.getTableId());
    }

    private DiningSession createTestSession() {
        Order completed = new Order("1", "10", "Test Branch", "Bàn B12",
                Order.Type.DINE_IN, Order.Status.COMPLETED, "18:48",
                new ArrayList<>(), 100000, "Đã hoàn thành");
        Order inProgress = new Order("2", "10", "Test Branch", "Bàn B12",
                Order.Type.DINE_IN, Order.Status.IN_PROGRESS, "19:05",
                new ArrayList<>(), 120000, "Đang chuẩn bị");
        Order placed = new Order("3", "10", "Test Branch", "Bàn B12",
                Order.Type.DINE_IN, Order.Status.PLACED, "19:24",
                new ArrayList<>(), 50000, "Đã tiếp nhận");

        return new DiningSession("10", "1", "VietTable Hoàn Kiếm", "Bàn B12",
                DiningSession.Status.ACTIVE, null, "Test Customer", "18:45",
                new ArrayList<>(Arrays.asList(completed, inProgress, placed)), new ArrayList<>());
    }
}
