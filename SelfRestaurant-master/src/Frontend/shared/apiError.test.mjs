import { test } from "node:test";
import assert from "node:assert/strict";
import { apiFailureMessage, cleanText, parseBody, userMessage } from "./apiError.ts";

test("cleanText drops values that mean nothing to a person", () => {
  for (const value of [null, undefined, "", "  ", "null", "undefined", "NaN", "[object Object]"]) {
    assert.equal(cleanText(value), "");
  }
  assert.equal(cleanText("  Hết hàng "), "Hết hàng");
});

test("an empty error body becomes a Vietnamese status message, never 'null'", () => {
  assert.equal(apiFailureMessage(500, parseBody("")), "Máy chủ đang gặp sự cố (mã 500). Vui lòng thử lại sau ít phút.");
  assert.equal(apiFailureMessage(502, parseBody("null")), "Máy chủ đang gặp sự cố (mã 502). Vui lòng thử lại sau ít phút.");
  assert.equal(apiFailureMessage(504, null), "Máy chủ phản hồi quá lâu (mã 504). Vui lòng thử lại.");
  assert.equal(apiFailureMessage(401, { message: null }), "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
});

test("the API's own message is kept; HTML error pages are not shown", () => {
  assert.equal(apiFailureMessage(400, { message: "Họ tên và số điện thoại là bắt buộc." }), "Họ tên và số điện thoại là bắt buộc.");
  assert.equal(apiFailureMessage(400, parseBody("Đơn không hợp lệ")), "Đơn không hợp lệ");
  assert.equal(apiFailureMessage(500, parseBody("<!doctype html><title>Error</title>")), "Máy chủ đang gặp sự cố (mã 500). Vui lòng thử lại sau ít phút.");
});

test("userMessage hides meaningless, network and JSON errors behind clear Vietnamese text", () => {
  assert.equal(userMessage(new Error("null"), "Không thể tải dữ liệu bếp."), "Không thể tải dữ liệu bếp.");
  assert.equal(userMessage(new TypeError("Failed to fetch"), "x"), "Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.");
  assert.equal(userMessage(new SyntaxError("Unexpected token '<'"), "Không thể tải."), "Không thể tải.");
  assert.equal(userMessage(new Error("Bàn đang có khách."), "x"), "Bàn đang có khách.");
  assert.equal(userMessage(undefined, "Đã có lỗi xảy ra."), "Đã có lỗi xảy ra.");
});
