package com.restaurant.viettable.data;

import org.junit.Test;
import static org.junit.Assert.assertEquals;

public class ServiceRequestTypeTest {
    @Test public void existingChipsUseSupportedTypes() {
        assertEquals("WATER", ServiceRequestType.normalize("Thêm nước lọc / Trà đá"));
        assertEquals("UTENSILS", ServiceRequestType.normalize("Lấy thêm bát đũa, khăn"));
        assertEquals("OTHER", ServiceRequestType.normalize("Thêm gia vị, nước chấm"));
        assertEquals("OTHER", ServiceRequestType.normalize("Kiểm tra món lâu lên"));
        assertEquals("OTHER", ServiceRequestType.normalize("Dọn bớt đĩa trống"));
        assertEquals("OTHER", ServiceRequestType.normalize("Yêu cầu khác"));
    }
    @Test public void noSelectionCallsStaff() {
        assertEquals("CALL_STAFF", ServiceRequestType.normalize("Hỗ trợ chung"));
        assertEquals("CALL_STAFF", ServiceRequestType.normalize(null));
    }
    @Test public void contractCodesArePreserved() {
        for (String code : new String[]{"CALL_STAFF", "WATER", "UTENSILS", "OTHER"})
            assertEquals(code, ServiceRequestType.normalize(code));
    }
}
