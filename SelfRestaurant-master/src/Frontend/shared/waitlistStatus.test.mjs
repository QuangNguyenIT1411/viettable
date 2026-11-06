import { test } from "node:test";
import assert from "node:assert/strict";
import { sortWaitlist, waitlistStatusLabel, waitlistTone } from "./waitlistStatus.ts";

const row = (id, status, created) => ({ waitlistEntryId: id, status, createdAtUtc: created, contactName: "", phoneNumber: "",
  partySize: 2, windowStartUtc: "", windowEndUtc: "", position: 0 });

test("holding first, then waiting by sign-up, then finished newest first", () => {
  const sorted = sortWaitlist([row(1, "Closed", "2026-11-20T01:00:00Z"), row(2, "Waiting", "2026-11-20T03:00:00Z"),
    row(3, "Offered", "2026-11-20T04:00:00Z"), row(4, "Waiting", "2026-11-20T02:00:00Z"), row(5, "Expired", "2026-11-20T05:00:00Z")]);
  assert.deepEqual(sorted.map(x => x.waitlistEntryId), [3, 4, 2, 5, 1]);
});

test("labels and tones", () => {
  assert.equal(waitlistStatusLabel("Offered"), "Đang giữ bàn");
  assert.equal(waitlistStatusLabel("Waiting"), "Đang chờ");
  assert.equal(waitlistTone("Offered"), "warning");
  assert.equal(waitlistTone("Cancelled"), "secondary");
});
