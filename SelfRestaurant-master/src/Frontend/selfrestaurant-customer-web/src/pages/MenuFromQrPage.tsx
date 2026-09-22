import { useEffect } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocation } from "react-router-dom";
import { api } from "../lib/api";
import { toMvcPath } from "../lib/mvcPaths";

const text = {
  loading: "Đang kiểm tra mã QR của bàn...",
  missingCode: "Không tìm thấy mã QR của bàn.",
  invalidCode: "Không tìm thấy bàn tương ứng với mã QR này.",
  settingContext: "Đang chuyển bạn đến bàn đã quét...",
} as const;

export function MenuFromQrPage() {
  const location = useLocation();
  const queryClient = useQueryClient();
  const code = new URLSearchParams(location.search).get("code")?.trim() ?? "";
  const session = useQuery({ queryKey: ["session"], queryFn: api.getSession });
  const tableLookup = useQuery({
    queryKey: ["menuFromQr", code],
    queryFn: () => api.getTableByQr(code),
    enabled: Boolean(code),
    retry: false,
  });

  const setContext = useMutation({
    mutationFn: api.setContextTable,
    onSuccess: async () => {
      await queryClient.invalidateQueries();
      window.location.assign(toMvcPath("/Menu/Index"));
    },
  });

  useEffect(() => {
    if (!code || session.isLoading || tableLookup.isLoading || setContext.isPending) {
      if (!code) {
        window.location.assign(toMvcPath("/Home/Index"));
      }
      return;
    }

    if (tableLookup.error) {
      window.location.assign(toMvcPath("/Home/Index"));
      return;
    }

    if (!tableLookup.data) {
      return;
    }

    const current = session.data?.tableContext;
    if (current?.tableId === tableLookup.data.tableId && current.branchId === tableLookup.data.branchId) {
      window.location.assign(toMvcPath("/Menu/Index"));
      return;
    }

    setContext.mutate({
      tableId: tableLookup.data.tableId,
      branchId: tableLookup.data.branchId,
    });
  }, [code, queryClient, session.data, session.isLoading, setContext, tableLookup.data, tableLookup.error, tableLookup.isLoading]);

  let message: string = text.loading;
  if (!code) {
    message = text.missingCode;
  } else if (tableLookup.error) {
    message = (tableLookup.error as Error).message || text.invalidCode;
  } else if (setContext.isPending) {
    message = text.settingContext;
  }

  return (
    <div className="container py-5 qr-entry-shell">
      <div className="card text-center p-4 qr-entry-card">
        <div className="qr-entry-icon" aria-hidden="true">
          <i className="fas fa-qrcode" />
        </div>
        <h3 className="mb-2">Self Restaurant</h3>
        <p className="text-muted mb-0">{message}</p>
      </div>
    </div>
  );
}
