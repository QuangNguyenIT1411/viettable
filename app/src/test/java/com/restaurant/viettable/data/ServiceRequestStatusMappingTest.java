package com.restaurant.viettable.data;

import com.restaurant.viettable.model.ServiceRequest;
import org.junit.Test;
import static org.junit.Assert.assertEquals;

public class ServiceRequestStatusMappingTest {
    @Test public void mapsOrdersApiServiceRequestStatesToCustomerLabels() {
        assertEquals(ServiceRequest.Status.REQUESTED, MobileRepository.serviceRequestStatus("OPEN"));
        assertEquals(ServiceRequest.Status.ACCEPTED, MobileRepository.serviceRequestStatus("ACKNOWLEDGED"));
        assertEquals(ServiceRequest.Status.COMPLETED, MobileRepository.serviceRequestStatus("COMPLETED"));
        assertEquals(ServiceRequest.Status.CANCELLED, MobileRepository.serviceRequestStatus("CANCELLED"));
    }
}
