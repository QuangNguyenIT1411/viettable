import { HubConnectionState } from "@microsoft/signalr";
import type { HubConnection } from "@microsoft/signalr";

type StaffConnection = Pick<HubConnection, "state" | "on" | "off" | "onreconnected" | "start" | "stop" | "invoke">;

// A reconnect has a new connection ID and no previous branch subscription.
// REST remains canonical; resync after joining to cover the disconnected interval.
export function bindStaffRealtime(
  connection: StaffConnection,
  branchId: number,
  refresh: () => void,
  reportError: (error: unknown) => void,
): () => void {
  let disposed = false;
  const refreshIfActive = () => { if (!disposed) refresh(); };
  const failIfActive = (error: unknown) => { if (!disposed) reportError(error); };
  const subscribeAndResync = async () => {
    if (disposed || connection.state !== HubConnectionState.Connected) return;
    await connection.invoke("SubscribeBranch", branchId);
    refreshIfActive();
  };

  connection.on("cashierDashboardChanged", refreshIfActive);
  connection.onreconnected(() => { void subscribeAndResync().catch(failIfActive); });
  void connection.start().then(subscribeAndResync).catch(failIfActive);

  return () => {
    disposed = true;
    connection.off("cashierDashboardChanged", refreshIfActive);
    // Server OnDisconnected cleans up membership. Do not race an unsubscribe
    // against a pending start/reconnect or leave a connection alive on failure.
    void connection.stop().catch(() => { /* already disposed */ });
  };
}
