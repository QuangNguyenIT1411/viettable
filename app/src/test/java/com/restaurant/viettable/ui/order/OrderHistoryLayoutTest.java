package com.restaurant.viettable.ui.order;

import org.junit.Test;
import org.w3c.dom.Document;
import org.w3c.dom.Element;
import org.w3c.dom.Node;
import org.w3c.dom.NodeList;

import java.io.File;
import javax.xml.parsers.DocumentBuilderFactory;
import static org.junit.Assert.*;

/** Guards the binding IDs used to render invoice loading, empty and retry states. */
public class OrderHistoryLayoutTest {
    private static final String ANDROID = "http://schemas.android.com/apk/res/android";

    @Test public void invoiceStateViewsBelongToVisibleHistoryContainer() throws Exception {
        DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
        factory.setNamespaceAware(true);
        factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true);
        File layout = new File("src/main/res/layout/fragment_order_history.xml");
        if (!layout.exists()) layout = new File("app/src/main/res/layout/fragment_order_history.xml");
        Document document = factory.newDocumentBuilder().parse(layout);
        for (String id : new String[]{"progressHistoryInvoices", "ivHistoryEmptyIcon",
                "tvHistoryEmptyMessage", "btnRetryHistoryInvoices"}) {
            Element element = null;
            NodeList nodes = document.getElementsByTagName("*");
            int matches = 0;
            for (int i = 0; i < nodes.getLength(); i++) {
                Element candidate = (Element) nodes.item(i);
                if (("@+id/" + id).equals(candidate.getAttributeNS(ANDROID, "id"))) {
                    element = candidate;
                    matches++;
                }
            }
            assertEquals(id + " must have one binding", 1, matches);
            Node parent = element.getParentNode();
            assertTrue(id + " needs a history-state parent", parent instanceof Element);
            assertEquals(id + " must not render into the hidden ongoing-session state",
                    "@+id/layoutEmptyPast", ((Element) parent).getAttributeNS(ANDROID, "id"));
        }
    }
}
