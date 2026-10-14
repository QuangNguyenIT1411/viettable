import type { Page, WebSocket } from "@playwright/test";

const HUB = "/hubs/customer-notifications";
// SignalR JSON protocol: a completion (type 3) answers the screen's SubscribeBranch call.
const isCompletion = (payload: string | Buffer) => /"type":3\b/.test(String(payload));

/**
 * Call BEFORE the screen opens. Resolves with the staff hub socket once the screen has joined its
 * branch, i.e. from then on changes are pushed instead of waiting for a poll.
 */
export async function staffRealtimeJoined(page: Page, timeout = 30_000): Promise<WebSocket> {
  const socket = await page.waitForEvent("websocket", { predicate: ws => ws.url().includes(HUB), timeout });
  await socket.waitForEvent("framereceived", { predicate: frame => isCompletion(frame.payload), timeout });
  return socket;
}

/** Resolves when the server pushes a branch change to this socket. */
export function branchChangePushed(socket: WebSocket, timeout = 15_000) {
  return socket.waitForEvent("framereceived", {
    predicate: frame => String(frame.payload).includes("staffBranchChanged"),
    timeout,
  });
}

/** Counts requests whose URL contains one of the given paths (e.g. polls). */
export function countRequests(page: Page, ...paths: string[]) {
  let count = 0;
  page.on("request", request => { if (paths.some(path => request.url().includes(path))) count++; });
  return () => count;
}

/** Makes the screen reload now (the same as returning to its tab), instead of waiting for the slow fallback poll. */
export async function reloadNow(page: Page) {
  await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));
}
