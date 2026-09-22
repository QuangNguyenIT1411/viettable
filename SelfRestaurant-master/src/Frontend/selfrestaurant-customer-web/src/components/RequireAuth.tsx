import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api } from "../lib/api";

export function RequireAuth({ children, requireTable = false, loginMessage }: { children: ReactNode; requireTable?: boolean; loginMessage?: string }) {
  const location = useLocation();
  const { data, isLoading } = useQuery({ queryKey: ["session"], queryFn: api.getSession });
  const from = `${location.pathname}${location.search}`;
  const loginParams = new URLSearchParams({ returnUrl: from });
  if (loginMessage) {
    loginParams.set("message", loginMessage);
    loginParams.set("type", "error");
  }

  if (isLoading) return <div className="screen-message">Đang tải phiên làm việc...</div>;
  if (!data?.authenticated) return <Navigate to={`/Customer/Login?${loginParams.toString()}`} replace state={{ from }} />;
  if (requireTable && !data.tableContext) return <Navigate to="/Home/Index" replace />;
  return <>{children}</>;
}
