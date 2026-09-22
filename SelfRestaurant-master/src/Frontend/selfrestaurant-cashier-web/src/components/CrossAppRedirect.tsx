import { useEffect } from "react";

type Props = {
  to: string;
  message: string;
};

export function CrossAppRedirect({ to, message }: Props) {
  useEffect(() => {
    // Staff areas live under different SPA basenames. Cross-role redirects
    // must leave the current shell instead of using in-app navigation.
    window.location.replace(to);
  }, [to]);

  return <div className="screen-message">{message}</div>;
}
