import { useEffect, useMemo } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, Navigate, useLocation } from "react-router-dom";
import { api } from "../lib/api";
import { MenuPage } from "./MenuPage";

function parsePositiveInt(value: string | null) {
  if (!value) return null;
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export function MenuIndexPage() {
  const location = useLocation();
  const queryClient = useQueryClient();
  const session = useQuery({ queryKey: ["session"], queryFn: api.getSession });
  const search = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const tableId = parsePositiveInt(search.get("tableId"));
  const branchId = parsePositiveInt(search.get("branchId"));

  const setContext = useMutation({
    mutationFn: api.setContextTable,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["session"] });
      await queryClient.invalidateQueries({ queryKey: ["menu"] });
      await queryClient.invalidateQueries({ queryKey: ["order"] });
      await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
    },
  });

  const shouldBootstrapFromQuery = Boolean(
    tableId
    && branchId
    && (
      !session.data?.tableContext
      || session.data.tableContext.tableId !== tableId
      || session.data.tableContext.branchId !== branchId
    ),
  );

  useEffect(() => {
    if (!shouldBootstrapFromQuery || setContext.isPending) {
      return;
    }

    setContext.mutate({ tableId: tableId!, branchId: branchId! });
  }, [branchId, setContext, shouldBootstrapFromQuery, tableId]);

  if (session.isLoading) {
    return <div className="screen-message">Đang tải phiên làm việc...</div>;
  }

  if (shouldBootstrapFromQuery || setContext.isPending) {
    return <div className="screen-message">Đang chuyển bạn đến bàn đã chọn...</div>;
  }

  if (setContext.error) {
    return (
      <div className="container py-4">
        <div className="error-box">
          <h3 className="mb-3">Không thể mở bàn đã chọn</h3>
          <p className="mb-3">{(setContext.error as Error).message}</p>
          <Link to="/Home/Index">Quay về trang chủ</Link>
        </div>
      </div>
    );
  }

  if (!session.data?.tableContext) {
    return <Navigate to="/Home/Index" replace />;
  }

  return <MenuPage />;
}
