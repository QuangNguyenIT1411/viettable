// Staff realtime: one hub connection per screen, joined to the employee's branch.
// Events carry no data. Screens reload through REST, which stays the source of truth.
// Kept free of the @microsoft/signalr import so every staff web can share it.

/** Everything a staff screen can be told to reload. "dashboard" is the cashier's older event. */
export const ALL_STAFF_TOPICS = ["dashboard", "orders", "onsitePayments", "serviceRequests"] as const;

export interface StaffHubConnection {
  readonly state: string;
  on(name: string, handler: (...args: any[]) => void): void;
  off(name: string, handler: (...args: any[]) => void): void;
  onreconnecting(handler: (error?: Error) => void): void;
  onreconnected(handler: (connectionId?: string) => void): void;
  onclose(handler: (error?: Error) => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(name: string, ...args: any[]): Promise<any>;
}

export function bindStaffRealtime(
  connection: StaffHubConnection,
  branchId: number,
  handlers: {
    /** Something under these topics changed. */
    changed: (topics: readonly string[]) => void;
    /** True once the branch is joined; false while disconnected (screens then poll faster). */
    live?: (live: boolean) => void;
    error: (error: unknown) => void;
  },
): () => void {
  let disposed = false;
  const changed = (topics: readonly string[]) => { if (!disposed) handlers.changed(topics); };
  const live = (value: boolean) => { if (!disposed) handlers.live?.(value); };
  const fail = (error: unknown) => { if (!disposed) { handlers.live?.(false); handlers.error(error); } };
  const onBranchChanged = (event?: { topics?: unknown }) => {
    const topics = Array.isArray(event?.topics) ? event!.topics.filter((t): t is string => typeof t === "string") : [];
    changed(topics.length > 0 ? topics : ALL_STAFF_TOPICS);
  };
  const onDashboardChanged = () => changed(["dashboard"]);
  // A reconnect has a new connection ID and no previous branch subscription.
  // Resync everything after joining to cover what was missed while disconnected.
  const subscribeAndResync = async () => {
    if (disposed || connection.state !== "Connected") return;
    await connection.invoke("SubscribeBranch", branchId);
    live(true);
    changed(ALL_STAFF_TOPICS);
  };

  connection.on("staffBranchChanged", onBranchChanged);
  connection.on("cashierDashboardChanged", onDashboardChanged);
  connection.onreconnecting(() => live(false));
  connection.onreconnected(() => { void subscribeAndResync().catch(fail); });
  connection.onclose(() => live(false));
  void connection.start().then(subscribeAndResync).catch(fail);

  return () => {
    disposed = true;
    connection.off("staffBranchChanged", onBranchChanged);
    connection.off("cashierDashboardChanged", onDashboardChanged);
    // Server OnDisconnected cleans up membership. Do not race an unsubscribe
    // against a pending start/reconnect or leave a connection alive on failure.
    void connection.stop().catch(() => { /* already disposed */ });
  };
}

/** Lets several panels on one screen share a single hub connection. */
export function createTopicBus() {
  const listeners = new Map<string, Set<() => void>>();
  return {
    on(topic: string, listener: () => void): () => void {
      const set = listeners.get(topic) ?? new Set();
      set.add(listener);
      listeners.set(topic, set);
      return () => { set.delete(listener); };
    },
    emit(topics: readonly string[]) {
      const called = new Set<() => void>();
      for (const topic of topics) {
        for (const listener of listeners.get(topic) ?? []) {
          if (called.has(listener)) continue;
          called.add(listener);
          listener();
        }
      }
    },
  };
}
export type TopicBus = ReturnType<typeof createTopicBus>;
