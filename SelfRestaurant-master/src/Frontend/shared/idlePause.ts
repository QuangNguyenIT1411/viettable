// Pauses background updates after a period without any user action, so an open but unattended
// screen stops waking the server. The screen resumes (reload + reconnect) when the user acts again.

export const IDLE_PAUSE_MS = 15 * 60 * 1000;

/** Deliberate actions. Mouse movement alone does not count. */
export const ACTIVITY_EVENTS = ["pointerdown", "keydown", "wheel", "touchstart"] as const;

export function createIdleMonitor(options: { timeout: number; idle: () => void }, environment = {
  now: () => Date.now(),
  schedule: (callback: () => void, delay: number) => setTimeout(callback, delay),
  cancel: (timer: ReturnType<typeof setTimeout>) => clearTimeout(timer),
}) {
  let last = environment.now();
  let timer: ReturnType<typeof setTimeout> | undefined;
  let stopped = false;
  const check = () => {
    timer = undefined;
    if (stopped) return;
    const remaining = options.timeout - (environment.now() - last);
    if (remaining > 0) {
      timer = environment.schedule(check, remaining);
      return;
    }
    stopped = true;
    options.idle();
  };
  timer = environment.schedule(check, options.timeout);
  return {
    /** Records a user action; cheap enough to call on every event. */
    activity() { last = environment.now(); },
    dispose() {
      stopped = true;
      if (timer !== undefined) environment.cancel(timer);
      timer = undefined;
    },
  };
}
