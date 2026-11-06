import { test } from "node:test";
import assert from "node:assert/strict";
import { actionHash, seenByStaff, senderLabel, supportStatusLabel, supportStatusTone, totalUnread, waitingText } from "./supportChat.ts";

test("status words", () => {
  assert.equal(supportStatusLabel("Waiting"), "Đang chờ");
  assert.equal(supportStatusLabel("InProgress"), "Đang xử lý");
  assert.equal(supportStatusLabel("Resolved"), "Đã giải quyết");
  assert.equal(supportStatusTone("Waiting"), "warning");
  assert.equal(supportStatusTone("Resolved"), "success");
});

test("how long the customer has waited", () => {
  const now = new Date("2026-11-07T10:00:00Z");
  assert.equal(waitingText("2026-11-07T09:59:40Z", now), "Vừa gửi");
  assert.equal(waitingText("2026-11-07T09:55:00", now), "Chờ 5 phút");
  assert.equal(waitingText("2026-11-07T07:50:00Z", now), "Chờ 2 giờ 10 phút");
  assert.equal(waitingText("2026-11-07T08:00:00Z", now), "Chờ 2 giờ");
  assert.equal(waitingText("2026-11-05T09:00:00Z", now), "Chờ 2 ngày");
  assert.equal(waitingText(null, now), "");
});

test("who wrote it", () => {
  assert.equal(senderLabel("AUTO"), "Tin nhắn tự động");
  assert.equal(senderLabel("STAFF", "Lan"), "Nhân viên Lan");
  assert.equal(senderLabel("CUSTOMER", null, "Quang2"), "Quang2");
});

test("seen markers and unread totals", () => {
  assert.equal(seenByStaff(5, 5), true);
  assert.equal(seenByStaff(6, 5), false);
  assert.equal(totalUnread([{ unread: 2, status: "Waiting" }, { unread: 1, status: "InProgress" }, { unread: 4, status: "Resolved" }]), 3);
});

test("links go only to known cashier tabs", () => {
  assert.equal(actionHash("dat-ban"), "#dat-ban");
  assert.equal(actionHash("chuyen-khoan"), "#chuyen-khoan");
  assert.equal(actionHash("javascript:alert(1)"), null);
  assert.equal(actionHash(null), null);
});
