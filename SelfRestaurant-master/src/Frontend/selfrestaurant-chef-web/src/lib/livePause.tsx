import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { ACTIVITY_EVENTS, IDLE_PAUSE_MS, createIdleMonitor } from "../../../shared/idlePause";

type LivePause = { paused: boolean; register: () => () => void };
const LivePauseContext = createContext<LivePause>({ paused: false, register: () => () => undefined });

/**
 * After 15 minutes without a user action, background work (polling, realtime) stops and a bar asks the
 * user to continue. Only screens with background work register, so other pages never show the bar.
 */
export function LivePauseProvider({ children }: { children: ReactNode }) {
  const [paused, setPaused] = useState(false);
  const [workers, setWorkers] = useState(0);
  const resumeButton = useRef<HTMLButtonElement>(null);
  const register = useCallback(() => {
    setWorkers(count => count + 1);
    return () => setWorkers(count => count - 1);
  }, []);
  const watching = workers > 0 && !paused;

  useEffect(() => {
    if (!watching) return;
    const monitor = createIdleMonitor({ timeout: IDLE_PAUSE_MS, idle: () => setPaused(true) });
    const activity = () => monitor.activity();
    for (const name of ACTIVITY_EVENTS) window.addEventListener(name, activity, { capture: true, passive: true });
    return () => {
      monitor.dispose();
      for (const name of ACTIVITY_EVENTS) window.removeEventListener(name, activity, { capture: true });
    };
  }, [watching]);

  const showBar = paused && workers > 0;
  useEffect(() => { if (showBar) resumeButton.current?.focus(); }, [showBar]);

  return (
    <LivePauseContext.Provider value={{ paused, register }}>
      {children}
      {showBar ? (
        <>
          {/* Covers the stale screen: the first click only resumes, it never acts on old data. */}
          <div className="vt-paused-overlay" aria-hidden="true" onClick={() => setPaused(false)} />
          <button ref={resumeButton} type="button" className="vt-paused-bar" onClick={() => setPaused(false)}>
            <i className="bi bi-pause-circle" aria-hidden="true" />
            Tạm dừng cập nhật để tiết kiệm. Bấm để tiếp tục
          </button>
        </>
      ) : null}
    </LivePauseContext.Provider>
  );
}

/** For background work. Returns true while paused; the caller must then stop polling and disconnect. */
export function useLivePaused(active = true) {
  const { paused, register } = useContext(LivePauseContext);
  useEffect(() => (active ? register() : undefined), [active, register]);
  return paused;
}
