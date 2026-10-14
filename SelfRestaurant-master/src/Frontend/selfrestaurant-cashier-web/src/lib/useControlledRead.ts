import { useCallback, useEffect, useRef } from "react";
import { createControlledRead } from "../../../shared/controlledRead";
import { useLivePaused } from "./livePause";

export function useControlledRead<T>(options: {
  read: (signal: AbortSignal) => Promise<T>;
  apply: (value: T) => void;
  error: (error: unknown) => void;
  settled?: () => void;
  /** Fallback poll delay in ms (0 = read on demand only). May change between renders. */
  interval: number;
}) {
  const latest = useRef(options);
  latest.current = options;
  const polling = options.interval > 0;
  // Stopped while paused for inactivity; recreated on resume, which reloads at once.
  const paused = useLivePaused(polling);
  const loop = useRef<ReturnType<typeof createControlledRead<T>> | null>(null);
  useEffect(() => {
    if (paused) return;
    const current = createControlledRead<T>({
      read: signal => latest.current.read(signal),
      apply: value => latest.current.apply(value),
      error: error => latest.current.error(error),
      settled: () => latest.current.settled?.(),
      interval: () => latest.current.interval,
    });
    loop.current = current;
    const visibility = () => current.visibilityChanged();
    document.addEventListener("visibilitychange", visibility);
    void current.refresh();
    return () => {
      document.removeEventListener("visibilitychange", visibility);
      current.dispose();
      if (loop.current === current) loop.current = null;
    };
  }, [polling, paused]);
  return useCallback(() => loop.current?.refresh() ?? Promise.resolve(), []);
}
