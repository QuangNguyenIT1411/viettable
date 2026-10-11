import { useCallback, useEffect, useRef } from "react";
import { createControlledRead } from "../../../shared/controlledRead";

export function useControlledRead<T>(options: {
  read: (signal: AbortSignal) => Promise<T>;
  apply: (value: T) => void;
  error: (error: unknown) => void;
  settled?: () => void;
  interval: number;
}) {
  const latest = useRef(options);
  latest.current = options;
  const loop = useRef<ReturnType<typeof createControlledRead<T>> | null>(null);
  useEffect(() => {
    const current = createControlledRead<T>({
      read: signal => latest.current.read(signal),
      apply: value => latest.current.apply(value),
      error: error => latest.current.error(error),
      settled: () => latest.current.settled?.(),
      interval: options.interval,
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
  }, [options.interval]);
  return useCallback(() => loop.current?.refresh() ?? Promise.resolve(), []);
}
