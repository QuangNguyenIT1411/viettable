package com.restaurant.viettable.assistant;

import java.text.Normalizer;
import java.time.DayOfWeek;
import java.time.LocalDate;
import java.time.LocalTime;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Reads a booking request in Vietnamese, with or without tone marks (owner 06/11): day ("hôm nay", "mai", "mốt",
 * "thứ 6", "chủ nhật", "15/11", "thứ 6 tuần sau"), part of day and hour ("tối", "7 giờ", "19h", "7g30", "7 rưỡi"),
 * party size ("4 người", "nhóm 6", "hai người lớn một trẻ em"), branch (by its name or district from the server's
 * list) and a note ("ghi chú ...", "sinh nhật"). Rules only, no paid AI. Anything not understood stays null so the
 * assistant asks for it; nothing is guessed.
 */
public final class BookingIntentParser {
    private BookingIntentParser() { }

    /** A branch as the server lists it. */
    public static final class BranchRef {
        public final String id;
        public final String name;
        public BranchRef(String id, String name) { this.id = id; this.name = name; }
    }

    private static final Map<String, Integer> NUMBER_WORDS = new HashMap<>();
    static {
        String[] words = {"khong", "mot", "hai", "ba", "bon", "nam", "sau", "bay", "tam", "chin", "muoi"};
        for (int i = 0; i < words.length; i++) NUMBER_WORDS.put(words[i], i);
        NUMBER_WORDS.put("tu", 4);
        NUMBER_WORDS.put("lam", 5);
    }

    private static final Pattern DATE = Pattern.compile("(?<![\\d:])(\\d{1,2})\\s*[/\\-.]\\s*(\\d{1,2})(?:\\s*[/\\-.]\\s*(\\d{2,4}))?(?![\\d:])");
    private static final Pattern DAY_ONLY = Pattern.compile("\\bngay (\\d{1,2})\\b(?!\\s*[/\\-.:])");
    private static final Pattern WEEKDAY_NUMBER = Pattern.compile("\\b(?:thu|t)\\s*([2-7])\\b");
    /** "7 giờ", "19h", "19h30", "7g30", "19:30", "7 giờ rưỡi", "7 rưỡi". A bare number is a party size or a date. */
    private static final Pattern TIME = Pattern.compile("(?<![\\d/.\\-])(?<h>\\d{1,2})\\s*(?:(?:h|g)(?<m1>\\d{2})?(?:\\s+(?<r1>ruoi))?|:(?<m2>\\d{2})|gio(?:\\s+(?<m3>\\d{2})(?:\\s*phut)?(?!\\s*(?:nguoi|khach|ng\\b))|\\s+(?<r2>ruoi))?|\\s+(?<r3>ruoi))(?![a-z\\d])");
    private static final Pattern PEOPLE_DIGITS = Pattern.compile("(?<![\\d:/])(\\d{1,2})\\s*(?:nguoi|nguo|ngoui|ng|khach|suat|pax|tre|be)(?![a-z])");
    private static final Pattern PEOPLE_GROUP = Pattern.compile("\\b(?:nhom|doan|cho|ban)\\s+(\\d{1,2})\\b(?!\\s*(?:gio\\b|h\\b|g\\b|:|/|nguoi|khach))");
    private static final Pattern PEOPLE_WORDS = Pattern.compile("\\b((?:muoi|mot|hai|ba|bon|tu|nam|sau|bay|tam|chin)(?: (?:mot|hai|ba|bon|tu|lam|nam|sau|bay|tam|chin))?) (?:nguoi|khach|be|tre)\\b");
    private static final Pattern NOTE = Pattern.compile("(?i)(ghi chú|ghi chu|note|lưu ý|luu y)\\s*[:\\-]?\\s*(.+)$");
    private static final Pattern NOTE_FOLDED = Pattern.compile("\\b(ghi chu|note|luu y)\\b");
    private static final Pattern DISTRICT = Pattern.compile("\\bq\\s*\\.?\\s*(\\d{1,2})\\b");

    /** Lower case, no tone marks, "đ" → "d", single spaces. */
    public static String fold(String text) {
        if (text == null) return "";
        String lower = text.toLowerCase(Locale.ROOT).replace('đ', 'd');
        String plain = Normalizer.normalize(lower, Normalizer.Form.NFD).replaceAll("\\p{M}", "");
        return plain.replaceAll("[^a-z0-9:/.,+\\- ]", " ").replaceAll("\\s+", " ").trim();
    }

    public static BookingIntent parse(String text, LocalDate today, List<BranchRef> branches) {
        BookingIntent intent = new BookingIntent();
        String original = text == null ? "" : text.trim();
        String s = fold(original);
        intent.note = note(original);
        // The note is read first and cut off, so "ghi chú: 2 ghế trẻ em" never changes the party size.
        Matcher noteMark = NOTE_FOLDED.matcher(s);
        if (noteMark.find()) s = s.substring(0, noteMark.start()).trim();
        intent.date = date(s, today);
        readTime(s, intent);
        intent.partySize = people(s);
        intent.branchId = branch(s, branches);
        return intent;
    }

    // ---------- day ----------

    static LocalDate date(String s, LocalDate today) {
        Matcher dm = DATE.matcher(s);
        while (dm.find()) {
            int day = Integer.parseInt(dm.group(1));
            int month = Integer.parseInt(dm.group(2));
            if (month < 1 || month > 12 || day < 1 || day > 31) continue;
            int year = dm.group(3) == null ? today.getYear() : Integer.parseInt(dm.group(3));
            if (year < 100) year += 2000;
            try {
                LocalDate d = LocalDate.of(year, month, day);
                if (dm.group(3) == null && d.isBefore(today)) d = d.plusYears(1);
                return d;
            } catch (Exception ignored) { }
        }
        if (has(s, "hom nay", "toi nay", "trua nay", "chieu nay", "sang nay", "bua nay")) return today;
        // "mốt" folds to "mot" like "một" (one): only in phrases that cannot mean a number.
        if (has(s, "ngay kia", "ngay mot", "toi mot", "trua mot", "chieu mot", "sang mot")) return today.plusDays(2);
        if (has(s, "mai")) return today.plusDays(1);
        DayOfWeek weekday = weekday(s);
        if (weekday != null) {
            int diff = (weekday.getValue() - today.getDayOfWeek().getValue() + 7) % 7;
            LocalDate d = today.plusDays(diff);
            if (has(s, "tuan sau", "tuan toi")) d = d.plusDays(7);
            return d;
        }
        Matcher onlyDay = DAY_ONLY.matcher(s);
        if (onlyDay.find()) {
            int day = Integer.parseInt(onlyDay.group(1));
            for (int add = 0; add < 2; add++) {
                try {
                    LocalDate d = today.plusMonths(add).withDayOfMonth(day);
                    if (!d.isBefore(today)) return d;
                } catch (Exception ignored) { }
            }
        }
        return null;
    }

    private static DayOfWeek weekday(String s) {
        if (has(s, "chu nhat", "cn")) return DayOfWeek.SUNDAY;
        Matcher m = WEEKDAY_NUMBER.matcher(s);
        if (m.find()) return DayOfWeek.of(Integer.parseInt(m.group(1)) - 1);
        String[][] names = {{"thu hai", "MONDAY"}, {"thu ba", "TUESDAY"}, {"thu tu", "WEDNESDAY"}, {"thu nam", "THURSDAY"},
                {"thu sau", "FRIDAY"}, {"thu bay", "SATURDAY"}};
        for (String[] n : names) if (has(s, n[0])) return DayOfWeek.valueOf(n[1]);
        return null;
    }

    // ---------- time ----------

    private static void readTime(String s, BookingIntent intent) {
        String period = null;
        if (has(s, "toi", "dem")) period = "toi";
        else if (has(s, "chieu")) period = "chieu";
        else if (has(s, "trua")) period = "trua";
        else if (has(s, "sang")) period = "sang";
        intent.period = period;
        Matcher m = TIME.matcher(s);
        while (m.find()) {
            int hour = Integer.parseInt(m.group("h"));
            String digits = m.group("m1") != null ? m.group("m1") : m.group("m2") != null ? m.group("m2") : m.group("m3");
            boolean half = m.group("r1") != null || m.group("r2") != null || m.group("r3") != null;
            int minute = digits != null ? Integer.parseInt(digits) : half ? 30 : 0;
            if (hour > 23 || minute > 59) continue;
            if (hour <= 11 && ("toi".equals(period) || "chieu".equals(period))) hour += 12;
            else if (hour <= 2 && "trua".equals(period)) hour += 12;
            // Without "sáng", 1–9 o'clock means afternoon/evening: the restaurant opens at 10:00.
            else if (period == null && hour >= 1 && hour <= 9) hour += 12;
            intent.time = LocalTime.of(hour, minute);
            return;
        }
    }

    // ---------- party ----------

    static Integer people(String s) {
        int total = 0;
        boolean any = false;
        Matcher digit = PEOPLE_DIGITS.matcher(s);
        while (digit.find()) { total += Integer.parseInt(digit.group(1)); any = true; }
        Matcher words = PEOPLE_WORDS.matcher(s);
        while (words.find()) {
            Integer n = wordNumber(words.group(1));
            if (n != null && n > 0) { total += n; any = true; }
        }
        if (!any) {
            Matcher group = PEOPLE_GROUP.matcher(s);
            if (group.find()) { total = Integer.parseInt(group.group(1)); any = true; }
        }
        if (!any) return has(s, "hai vo chong", "2 vo chong", "vo chong toi", "cap doi") ? Integer.valueOf(2) : null;
        return total >= 1 && total <= 30 ? total : null;
    }

    private static Integer wordNumber(String text) {
        String[] parts = text.trim().split(" ");
        if (parts.length == 1) return NUMBER_WORDS.get(parts[0]);
        if ("muoi".equals(parts[0])) {
            Integer unit = NUMBER_WORDS.get(parts[1]);
            return unit == null ? null : 10 + unit;
        }
        return null;
    }

    // ---------- branch ----------

    static String branch(String s, List<BranchRef> branches) {
        if (branches == null) return null;
        String text = " " + DISTRICT.matcher(s).replaceAll("quan $1").replaceAll("[,.;]", " ").replaceAll("\\s+", " ") + " ";
        String best = null;
        int bestScore = 0;
        for (BranchRef b : branches) {
            String name = fold(b.name).replace("chi nhanh", "").trim();
            if (name.isEmpty()) continue;
            int score = text.contains(" " + name + " ") ? 3 : closeMatch(text, name) ? 2 : 0;
            if (score > bestScore) { bestScore = score; best = b.id; }
        }
        return best;
    }

    /** One small slip per long word ("binh tanh" for "binh thanh", "go vap" for "gò vấp" is exact once folded). */
    private static boolean closeMatch(String text, String name) {
        String[] want = name.split(" ");
        String[] words = text.trim().split(" ");
        for (int i = 0; i + want.length <= words.length; i++) {
            boolean ok = true;
            for (int j = 0; j < want.length && ok; j++) {
                String a = words[i + j];
                String b = want[j];
                ok = a.equals(b) || (b.length() >= 4 && distance(a, b) <= 1);
            }
            if (ok) return true;
        }
        return false;
    }

    private static int distance(String a, String b) {
        int[] prev = new int[b.length() + 1];
        int[] cur = new int[b.length() + 1];
        for (int j = 0; j <= b.length(); j++) prev[j] = j;
        for (int i = 1; i <= a.length(); i++) {
            cur[0] = i;
            for (int j = 1; j <= b.length(); j++)
                cur[j] = Math.min(Math.min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a.charAt(i - 1) == b.charAt(j - 1) ? 0 : 1));
            int[] t = prev; prev = cur; cur = t;
        }
        return prev[b.length()];
    }

    // ---------- note ----------

    static String note(String original) {
        Matcher m = NOTE.matcher(original);
        if (m.find()) {
            String note = m.group(2).trim();
            return note.isEmpty() ? null : (note.length() > 500 ? note.substring(0, 500) : note);
        }
        String s = fold(original);
        List<String> found = new ArrayList<>();
        if (has(s, "sinh nhat")) found.add("Sinh nhật");
        if (has(s, "ky niem")) found.add("Kỷ niệm");
        if (has(s, "ghe tre em", "ghe em be")) found.add("Cần ghế trẻ em");
        if (has(s, "cua so")) found.add("Gần cửa sổ");
        if (has(s, "yen tinh")) found.add("Chỗ yên tĩnh");
        return found.isEmpty() ? null : String.join(", ", found);
    }

    /** Whole words or phrases, ignoring commas and dots around them. */
    private static boolean has(String s, String... keys) {
        String text = " " + s.replaceAll("[,.;]", " ").replaceAll("\\s+", " ").trim() + " ";
        for (String k : keys) if (text.contains(" " + k.trim() + " ")) return true;
        return false;
    }
}
