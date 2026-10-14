import { HttpTransportType, HubConnectionBuilder } from "@microsoft/signalr";
import { useEffect, useRef } from "react";
import { bindStaffRealtime } from "../../../shared/staffRealtime";
import { useLivePaused } from "./livePause";

/** Branch realtime for this screen; disconnected while the screen is paused for inactivity. */
export function useStaffRealtime(branchId: number | null | undefined, handlers: {
  changed: (topics: readonly string[]) => void;
  live?: (live: boolean) => void;
  error?: (error: unknown) => void;
}) {
  const latest = useRef(handlers);
  latest.current = handlers;
  const paused = useLivePaused(Boolean(branchId));
  useEffect(() => {
    if (!branchId || paused) return;
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/customer-notifications", {
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .withAutomaticReconnect()
      .build();
    const dispose = bindStaffRealtime(connection, branchId, {
      changed: topics => latest.current.changed(topics),
      live: value => latest.current.live?.(value),
      error: error => latest.current.error?.(error),
    });
    return () => {
      dispose();
      latest.current.live?.(false);
    };
  }, [branchId, paused]);
}
