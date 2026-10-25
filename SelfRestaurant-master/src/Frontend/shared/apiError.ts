// Error text shown to staff. Never "null"/"undefined"/"NaN"/empty: fall back to a Vietnamese sentence instead.

const MEANINGLESS = new Set(["", "null", "undefined", "nan", "[object object]"]);

/** Returns the trimmed text, or "" when it carries no information for a person. */
export function cleanText(value: unknown): string {
  if (value === null || value === undefined) return "";
  const text = String(value).trim();
  return MEANINGLESS.has(text.toLowerCase()) ? "" : text;
}

/**
 * A raw technical English sentence (e.g. "Response status code does not indicate success: 409") that must never
 * reach staff or guests: plain ASCII with several English words. Our own messages are Vietnamese with diacritics.
 */
export function isTechnicalEnglish(text: string): boolean {
  return /^[\x20-\x7E]+$/.test(text) && /[A-Za-z]{3,}\s+[A-Za-z]{2,}\s+[A-Za-z]{2,}/.test(text);
}

/** Vietnamese message for an HTTP failure whose body has no usable message. */
export function httpStatusMessage(status: number): string {
  if (status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  if (status === 403) return "Bạn không có quyền thực hiện thao tác này.";
  if (status === 404) return "Không tìm thấy dữ liệu yêu cầu.";
  if (status === 408 || status === 504) return `Máy chủ phản hồi quá lâu (mã ${status}). Vui lòng thử lại.`;
  if (status === 429) return "Có quá nhiều yêu cầu. Vui lòng đợi một chút rồi thử lại.";
  if (status >= 500) return `Máy chủ đang gặp sự cố (mã ${status}). Vui lòng thử lại sau ít phút.`;
  return `Yêu cầu không thành công (mã ${status}). Vui lòng thử lại.`;
}

/** Reads a response body as JSON when possible, otherwise as text; never throws. */
export function parseBody(text: string): unknown {
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

/** Message for a failed response: the API's own message when it has one, otherwise one based on the status. */
export function apiFailureMessage(status: number, payload: unknown): string {
  if (payload && typeof payload === "object") {
    const message = cleanText((payload as { message?: unknown }).message);
    if (message && !isTechnicalEnglish(message)) return message;
  } else if (typeof payload === "string") {
    const text = cleanText(payload);
    // A short plain-text body is a message; HTML error pages and long dumps are not.
    if (text && text.length <= 300 && !/^\s*</.test(text) && !isTechnicalEnglish(text)) return text;
  }
  return httpStatusMessage(status);
}

/** Message for any caught error, for display in an error box. */
export function userMessage(error: unknown, fallback: string): string {
  if (error instanceof Error) {
    const message = cleanText(error.message);
    if (/failed to fetch|networkerror|load failed|network request failed/i.test(message)) {
      return "Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.";
    }
    if (error.name === "SyntaxError" || /^Unexpected token|is not valid JSON/i.test(message)) return fallback;
    if (isTechnicalEnglish(message)) return fallback;
    return message || fallback;
  }
  return cleanText(error) || fallback;
}
