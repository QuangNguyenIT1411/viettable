package com.restaurant.viettable.util;

/** What a guest may read: never a raw technical English sentence (incident 25/10: "Response status code does not indicate success"). */
public final class UserText {
    private UserText() { }

    /** Plain ASCII with several English words. Our own messages are Vietnamese with diacritics. */
    public static boolean isTechnicalEnglish(String text) {
        if (text == null) return false;
        String value = text.trim();
        return !value.isEmpty() && value.matches("[\\x20-\\x7E]+")
                && value.matches(".*[A-Za-z]{3,}\\s+[A-Za-z]{2,}\\s+[A-Za-z]{2,}.*");
    }

    /** The server's message when a guest can read it, otherwise a Vietnamese sentence for the HTTP status. */
    public static String serverMessage(String message, int status) {
        String clean = SafeText.clean(message);
        if (!clean.isEmpty() && !isTechnicalEnglish(clean)) return clean;
        return statusMessage(status);
    }

    public static String statusMessage(int status) {
        if (status == 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        if (status == 403) return "Bạn không có quyền thực hiện thao tác này.";
        if (status == 404) return "Không tìm thấy dữ liệu yêu cầu.";
        if (status == 409) return "Thao tác không thực hiện được ở trạng thái hiện tại. Vui lòng tải lại và thử lại.";
        if (status == 429) return "Có quá nhiều yêu cầu. Vui lòng đợi một chút rồi thử lại.";
        if (status >= 500) return "Máy chủ đang gặp sự cố. Vui lòng thử lại sau ít phút.";
        return "Yêu cầu không thành công. Vui lòng thử lại.";
    }
}
