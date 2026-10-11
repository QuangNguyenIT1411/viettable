// Read-only synchronization: one request at a time, one trailing resync after event bursts.
// Mutations are deliberately not retried or scheduled here.
export function createControlledRead<T>(options: {
  read: (signal: AbortSignal) => Promise<T>;
  apply: (value: T) => void;
  error: (error: unknown) => void;
  settled?: () => void;
  interval: number;
}, environment = {
  visible: () => document.visibilityState !== "hidden",
  schedule: (callback: () => void, delay: number) => setTimeout(callback, delay),
  cancel: (timer: ReturnType<typeof setTimeout>) => clearTimeout(timer),
}) {
  let disposed = false;
  let requested = false;
  let failures = 0;
  let pending: Promise<void> | null = null;
  let controller: AbortController | null = null;
  let timer: ReturnType<typeof setTimeout> | undefined;
  const cancelTimer = () => { if (timer !== undefined) environment.cancel(timer); timer = undefined; };
  const refresh = (): Promise<void> => {
    if (disposed) return Promise.resolve();
    requested = true;
    cancelTimer();
    if (pending) return pending;
    if (!environment.visible()) return Promise.resolve();
    pending = (async () => {
      // Start on a microtask, so pending is assigned even with a synchronous read failure.
      await Promise.resolve();
      while (!disposed && requested && environment.visible()) {
        requested = false;
        controller = new AbortController();
        try {
          const value = await options.read(controller.signal);
          if (!disposed && !controller.signal.aborted) { failures = 0; options.apply(value); }
        } catch (error) {
          if (!disposed && !controller.signal.aborted) { failures++; options.error(error); }
          // Do not immediately replay a failed request because events arrived while it ran.
          requested = false;
        } finally {
          if (!disposed) options.settled?.();
        }
      }
    })().finally(() => {
      pending = null;
      if (!disposed && options.interval > 0 && environment.visible()) {
        timer = environment.schedule(() => { void refresh(); },
          Math.min(options.interval * 2 ** Math.min(failures, 4), 60000));
      }
    });
    return pending;
  };
  return {
    refresh,
    visibilityChanged: () => { cancelTimer(); if (environment.visible()) void refresh(); },
    dispose: () => { disposed = true; requested = false; cancelTimer(); controller?.abort(); },
  };
}
