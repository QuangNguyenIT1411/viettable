import { useCallback, useEffect, useRef, useState } from "react";

export function useAutoDismissMessage(durationMs = 5000): [string | null, (nextMessage: string | null) => void] {
  const [message, setMessageState] = useState<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearTimer = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const setMessage = useCallback((nextMessage: string | null) => {
    clearTimer();
    setMessageState(nextMessage);

    if (nextMessage) {
      timerRef.current = setTimeout(() => {
        setMessageState(null);
        timerRef.current = null;
      }, durationMs);
    }
  }, [clearTimer, durationMs]);

  useEffect(() => clearTimer, [clearTimer]);

  return [message, setMessage];
}
