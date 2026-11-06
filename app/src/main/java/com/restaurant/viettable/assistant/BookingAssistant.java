package com.restaurant.viettable.assistant;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.Locale;

/**
 * The booking conversation (owner 06/11): keeps what the guest said, asks for one missing thing at a time (branch,
 * day, hour, party), then checks the tables with the server and books only after the guest confirms. Pure; the
 * screen only shows its turns and calls the server when asked to.
 */
public final class BookingAssistant {
    public enum Action { NONE, CHECK_AVAILABILITY, CREATE }

    public static final class Turn {
        public final String message;
        public final List<String> quickReplies;
        public final Action action;
        Turn(String message, List<String> quickReplies, Action action) {
            this.message = message;
            this.quickReplies = quickReplies == null ? Collections.emptyList() : quickReplies;
            this.action = action;
        }
    }

    /** Booking hours from the server (GET /reservations/hours); null until loaded, then the server checks alone. */
    private LocalTime openFrom;
    private LocalTime lastSlot;
    private int slotMinutes = 30;

    public void setHours(LocalTime openFrom, LocalTime lastSlot, int slotMinutes) {
        this.openFrom = openFrom;
        this.lastSlot = lastSlot;
        if (slotMinutes > 0) this.slotMinutes = slotMinutes;
    }

    /** The bookable time nearest to {@code t} (owner 06/11: suggested, never applied by itself). */
    static LocalTime nearestSlot(LocalTime t, LocalTime open, LocalTime last, int slotMinutes) {
        if (t.isBefore(open)) return open;
        if (t.isAfter(last)) return last;
        int fromOpen = (t.toSecondOfDay() - open.toSecondOfDay()) / 60;
        int rounded = Math.round(fromOpen / (float) slotMinutes) * slotMinutes;
        LocalTime slot = open.plusMinutes(rounded);
        return slot.isAfter(last) ? last : slot;
    }

    private final List<BookingIntentParser.BranchRef> branches;
    private final BookingIntent intent = new BookingIntent();
    private boolean awaitingConfirmation;

    public BookingAssistant(List<BookingIntentParser.BranchRef> branches) {
        this.branches = branches == null ? Collections.emptyList() : new ArrayList<>(branches);
    }

    public BookingIntent intent() { return intent; }

    public Turn greeting() {
        return new Turn("Chào bạn! Bạn muốn đặt bàn khi nào, mấy người, ở chi nhánh nào? Ví dụ: “Tối mai 7 giờ, 4 người, Quận 1”.",
                Arrays.asList("Tối nay 7 giờ 2 người", "Ngày mai 19h 4 người"), Action.NONE);
    }

    /** One message from the guest. */
    public Turn reply(String text, LocalDateTime now) {
        String folded = BookingIntentParser.fold(text).replaceAll("[,.!?;]", " ").replaceAll("\\s+", " ").trim();
        if (awaitingConfirmation) {
            if (isYes(folded)) { awaitingConfirmation = false; return new Turn("Đang đặt bàn cho bạn…", null, Action.CREATE); }
            if (isNo(folded)) {
                awaitingConfirmation = false;
                return new Turn("Bạn muốn đổi gì? Nhắn lại ngày, giờ, số người hoặc chi nhánh.", null, Action.NONE);
            }
        }
        BookingIntent said = BookingIntentParser.parse(text, now.toLocalDate(), branches);
        // "sáng mai" then "8 giờ": the hour keeps the part of day said before.
        if (said.time != null && said.period == null && intent.period != null && said.typedHour >= 0)
            said.time = BookingIntentParser.withPeriod(said.typedHour, said.typedMinute, intent.period);
        boolean understood = said.date != null || said.time != null || said.partySize != null || said.branchId != null
                || said.period != null || said.note != null;
        awaitingConfirmation = false;
        intent.merge(said);
        String problem = problem(now);
        if (problem != null && problem.startsWith("!"))
            return new Turn(problem.substring(1),
                    Arrays.asList(outsideHoursSuggestion.format(DateTimeFormatter.ofPattern("HH:mm"))), Action.NONE);
        if (problem != null) return problem.startsWith("?") ? ask(problem.substring(1)) : new Turn(problem, null, Action.NONE);
        Turn next = nextQuestion();
        if (next != null) return understood || intent.complete() ? next
                : new Turn("Mình chưa hiểu câu này. " + next.message, next.quickReplies, Action.NONE);
        return new Turn("Để mình kiểm tra bàn: " + summary() + ".", null, Action.CHECK_AVAILABILITY);
    }

    /** The server found room: ask the guest to confirm before booking. */
    public Turn availableNow() {
        awaitingConfirmation = true;
        return new Turn("Còn bàn! " + summary() + ". Bạn xác nhận đặt bàn nhé?", Arrays.asList("Đặt bàn", "Đổi thông tin"), Action.NONE);
    }

    /** The server found no room: the guest picks a time or branch it suggested (labels from the server). */
    public Turn full(List<String> suggestionLabels) {
        awaitingConfirmation = false;
        if (suggestionLabels == null || suggestionLabels.isEmpty())
            return new Turn("Rất tiếc, khung giờ này đã hết bàn và chưa có giờ khác phù hợp trong ngày. Bạn thử ngày khác nhé.",
                    null, Action.NONE);
        return new Turn("Rất tiếc, khung giờ này đã hết bàn. Bạn chọn một gợi ý dưới đây, hoặc nhắn giờ khác:", suggestionLabels, Action.NONE);
    }

    /** A suggestion was chosen: another time (same branch) or another branch (same time). */
    public Turn choose(String branchId, LocalDate date, LocalTime time) {
        if (branchId != null) intent.branchId = branchId;
        if (date != null) intent.date = date;
        if (time != null) intent.time = time;
        return new Turn("Để mình kiểm tra bàn: " + summary() + ".", null, Action.CHECK_AVAILABILITY);
    }

    public String summary() {
        StringBuilder text = new StringBuilder();
        text.append(branchName(intent.branchId)).append(" · ").append(dayText(intent.date)).append(" · ")
                .append(intent.time == null ? "" : intent.time.format(DateTimeFormatter.ofPattern("HH:mm")))
                .append(" · ").append(intent.partySize).append(" người");
        if (intent.note != null) text.append(" · Ghi chú: ").append(intent.note);
        return text.toString();
    }

    // ---------- helpers ----------

    private String problem(LocalDateTime now) {
        if (intent.date != null && intent.date.isBefore(now.toLocalDate())) {
            intent.date = null;
            return "?Ngày này đã qua. Bạn muốn đặt ngày nào ạ?";
        }
        if (outsideHours()) {
            outsideHoursSuggestion = nearestSlot(intent.time, openFrom, lastSlot, slotMinutes);
            String said = intent.time.format(DateTimeFormatter.ofPattern("HH:mm"));
            intent.time = null;
            return "!" + "Nhà hàng chỉ nhận đặt bàn từ " + openFrom.format(DateTimeFormatter.ofPattern("HH:mm")) + " đến "
                    + lastSlot.format(DateTimeFormatter.ofPattern("HH:mm")) + ", nên chưa đặt được lúc " + said
                    + ". Giờ gần nhất là " + outsideHoursSuggestion.format(DateTimeFormatter.ofPattern("HH:mm"))
                    + ". Bạn chọn giờ này hoặc nhắn giờ khác nhé.";
        }
        if (intent.date != null && intent.time != null && intent.date.atTime(intent.time).isBefore(now.plusMinutes(15))) {
            intent.time = null;
            return "?Giờ này đã qua hoặc quá sát. Bạn chọn giờ muộn hơn nhé?";
        }
        if (intent.partySize != null && (intent.partySize < 1 || intent.partySize > 30)) {
            intent.partySize = null;
            return "?Nhà hàng nhận đặt bàn từ 1 đến 30 người. Bạn đi mấy người ạ?";
        }
        return null;
    }

    private LocalTime outsideHoursSuggestion;

    private boolean outsideHours() {
        if (intent.time == null || openFrom == null || lastSlot == null) return false;
        return intent.time.isBefore(openFrom) || intent.time.isAfter(lastSlot);
    }

    private Turn ask(String message) {
        Turn next = nextQuestion();
        return new Turn(message, next == null ? null : next.quickReplies, Action.NONE);
    }

    private Turn nextQuestion() {
        if (intent.branchId == null) {
            List<String> names = new ArrayList<>();
            for (BookingIntentParser.BranchRef b : branches) names.add(b.name);
            return new Turn("Bạn muốn đặt ở chi nhánh nào ạ?", names, Action.NONE);
        }
        if (intent.date == null) return new Turn("Bạn muốn đặt ngày nào ạ?", Arrays.asList("Hôm nay", "Ngày mai", "Thứ 7", "Chủ nhật"), Action.NONE);
        if (intent.time == null) {
            List<String> hours = hoursFor(intent.period);
            return new Turn(intent.period == null ? "Bạn đến lúc mấy giờ ạ?" : "Mấy giờ ạ?", hours, Action.NONE);
        }
        if (intent.partySize == null) return new Turn("Bạn đi mấy người ạ?", Arrays.asList("2 người", "4 người", "6 người"), Action.NONE);
        return null;
    }

    /** Quick replies for "Mấy giờ ạ?" inside the part of day said and the booking hours (when known). */
    private List<String> hoursFor(String period) {
        LocalTime from, to;
        if ("sang".equals(period)) { from = LocalTime.of(6, 0); to = LocalTime.of(11, 30); }
        else if ("trua".equals(period)) { from = LocalTime.of(11, 0); to = LocalTime.of(13, 30); }
        else if ("chieu".equals(period)) { from = LocalTime.of(14, 0); to = LocalTime.of(17, 30); }
        else { from = LocalTime.of(18, 0); to = LocalTime.of(20, 30); }
        if (openFrom != null && from.isBefore(openFrom)) from = openFrom;
        if (lastSlot != null && to.isAfter(lastSlot)) to = lastSlot;
        List<String> out = new ArrayList<>();
        for (LocalTime t = from; !t.isAfter(to) && out.size() < 5; t = t.plusMinutes(slotMinutes)) out.add(t.format(DateTimeFormatter.ofPattern("HH:mm")));
        return out;
    }

    private String branchName(String id) {
        for (BookingIntentParser.BranchRef b : branches) if (b.id.equals(id)) return b.name;
        return "Chi nhánh";
    }

    private static String dayText(LocalDate date) {
        if (date == null) return "";
        String[] names = {"Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ Nhật"};
        return names[date.getDayOfWeek().getValue() - 1] + " " + date.format(DateTimeFormatter.ofPattern("dd/MM", Locale.ROOT));
    }

    private static boolean isYes(String s) {
        return s.matches("^(co|vang|da|dung|ok|oke|okay|uh|u|dat|dat ban|dat di|xac nhan|chot|duoc|yes)( .*)?$");
    }

    private static boolean isNo(String s) {
        return s.matches("^(khong|ko|k|doi|doi thong tin|thoi|huy|sua|no)( .*)?$");
    }
}
