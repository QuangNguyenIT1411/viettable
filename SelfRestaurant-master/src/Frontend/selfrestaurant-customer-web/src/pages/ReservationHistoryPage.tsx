import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PublicNavbar } from "../components/PublicNavbar";
import { api } from "../lib/api";
import type { ReservationDto } from "../lib/types";
import { toMvcPath } from "../lib/mvcPaths";

const statusLabels: Record<string, string> = {
  Pending: "Chờ xác nhận",
  Confirmed: "Đã xác nhận",
  CheckingIn: "Đang check-in",
  CheckedIn: "Đã check-in",
  Cancelled: "Đã hủy",
  NoShow: "Không đến",
  Completed: "Hoàn tất",
};

const preOrderStatuses = new Set(["Pending", "Confirmed"]);
const cancelStatuses = new Set(["Pending", "Confirmed"]);

function formatDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat("vi-VN", { dateStyle: "medium", timeStyle: "short" }).format(date);
}

function preOrderCount(reservation: ReservationDto) {
  return reservation.preOrderItems
    .filter((item) => !["Cancelled"].includes(item.status))
    .reduce((sum, item) => sum + item.quantity, 0);
}

function resolveAssignedTable(reservation: ReservationDto) {
  const assignedTables = reservation.assignedTables ?? [];
  const primaryTable = assignedTables.find((table) => table.isPrimary) ?? assignedTables[0];
  const tableId = primaryTable?.tableId ?? reservation.tableId ?? null;
  const mergedTableIds = assignedTables.map((table) => table.tableId).filter((tableId) => tableId > 0);

  return { tableId, mergedTableIds };
}

export function ReservationHistoryPage() {
  const queryClient = useQueryClient();
  const reservations = useQuery({
    queryKey: ["myReservations"],
    queryFn: api.getMyReservations,
  });

  const cancelReservation = useMutation({
    mutationFn: (reservationId: number) => api.cancelReservation(reservationId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["myReservations"] });
    },
  });

  const enterTable = useMutation({
    mutationFn: ({ tableId, branchId }: { tableId: number; branchId: number }) => api.setContextTable({ tableId, branchId }),
    onSuccess: async () => {
      await queryClient.invalidateQueries();
      window.location.assign(toMvcPath("/Menu/Index"));
    },
  });

  return (
    <div className="reservation-page">
      <PublicNavbar />
      <main className="home-container reservation-history-shell">
        <section className="reservation-history-hero">
          <span className="reservation-kicker"><i className="fas fa-calendar-check" /> Lịch sử đặt bàn</span>
          <h1>Lịch sử đặt bàn</h1>
          <p>Theo dõi các lần đặt bàn của bạn, mở món đặt trước hoặc hủy đặt bàn khi còn được phép.</p>
          <Link to="/Reservation/Index" className="btn btn-danger">
            <i className="fas fa-plus me-2" />
            Đặt bàn mới
          </Link>
        </section>

        {reservations.isLoading ? (
          <div className="reservation-card reservation-history-empty">Đang tải lịch sử đặt bàn...</div>
        ) : null}

        {reservations.error ? (
          <div className="reservation-alert reservation-alert-error">Không thể tải lịch sử đặt bàn. Vui lòng thử lại.</div>
        ) : null}

        {cancelReservation.error ? (
          <div className="reservation-alert reservation-alert-error">
            {cancelReservation.error instanceof Error ? cancelReservation.error.message : "Không thể hủy đặt bàn."}
          </div>
        ) : null}

        {enterTable.error ? (
          <div className="reservation-alert reservation-alert-error">
            {enterTable.error instanceof Error ? enterTable.error.message : "Không thể vào bàn. Vui lòng thử lại."}
          </div>
        ) : null}

        {!reservations.isLoading && !reservations.error && (reservations.data?.length ?? 0) === 0 ? (
          <div className="reservation-card reservation-history-empty">
            <i className="fas fa-calendar-plus" />
            <strong>Chưa có đặt bàn nào</strong>
            <p>Hãy tạo đặt bàn mới để nhà hàng chuẩn bị tốt hơn cho bạn.</p>
            <Link to="/Reservation/Index" className="btn btn-danger">Đặt bàn ngay</Link>
          </div>
        ) : null}

        {reservations.data && reservations.data.length > 0 ? (
          <section className="reservation-history-list">
            {reservations.data.map((reservation) => {
              const canOpenPreOrder = preOrderStatuses.has(reservation.status);
              const canCancel = cancelStatuses.has(reservation.status);
              const isCheckedIn = reservation.status === "CheckedIn";
              const assignedTable = resolveAssignedTable(reservation);
              const canEnterTable = Boolean(isCheckedIn && assignedTable.tableId && reservation.branchId > 0);
              const mergedTableText = assignedTable.mergedTableIds.length > 1 ? assignedTable.mergedTableIds.join(", ") : null;

              return (
                <article key={reservation.reservationId} className="reservation-card reservation-history-card">
                  <div className="reservation-history-main">
                    <div>
                      <span className="reservation-kicker">Mã đặt bàn</span>
                      <h2>{reservation.reservationCode}</h2>
                    </div>
                    <span className={`reservation-history-status reservation-history-status-${reservation.status.toLowerCase()}`}>
                      {statusLabels[reservation.status] ?? reservation.status}
                    </span>
                  </div>

                  <div className="reservation-history-grid">
                    <Info label="Thời gian" value={formatDateTime(reservation.reservedAt)} />
                    <Info label="Số khách" value={`${reservation.partySize} người`} />
                    <Info label="Món đặt trước" value={`${preOrderCount(reservation)} món`} />
                    <Info label="Khách hàng" value={reservation.customerName} />
                  </div>

                  {reservation.note ? <p className="reservation-history-note"><strong>Ghi chú:</strong> {reservation.note}</p> : null}

                  {isCheckedIn ? (
                    <div className="reservation-enter-table">
                      {assignedTable.tableId ? (
                        <>
                          <div className="reservation-enter-table-copy">
                            <strong>Bạn đã được nhận bàn. Nhấn để gọi món.</strong>
                            <span>Bàn chính: {assignedTable.tableId}</span>
                            {mergedTableText ? <span>Bàn ghép: {mergedTableText}</span> : null}
                          </div>
                          <button
                            type="button"
                            className="btn btn-danger"
                            disabled={!canEnterTable || enterTable.isPending}
                            onClick={() => {
                              if (!canEnterTable || !assignedTable.tableId) return;
                              enterTable.mutate({ tableId: assignedTable.tableId, branchId: reservation.branchId });
                            }}
                          >
                            {enterTable.isPending ? "Đang vào bàn..." : "Vào bàn"}
                          </button>
                        </>
                      ) : (
                        <div className="reservation-enter-table-copy">
                          <strong>Nhà hàng chưa sắp xếp bàn</strong>
                        </div>
                      )}
                    </div>
                  ) : null}

                  <div className="reservation-history-actions">
                    {canOpenPreOrder ? (
                      <Link className="btn btn-outline-danger" to={`/Reservation/${encodeURIComponent(reservation.reservationCode)}/PreOrder`}>
                        Mở món đặt trước
                      </Link>
                    ) : null}
                    {canCancel ? (
                      <button
                        type="button"
                        className="btn btn-outline-secondary"
                        onClick={() => cancelReservation.mutate(reservation.reservationId)}
                        disabled={cancelReservation.isPending}
                      >
                        {cancelReservation.isPending ? "Đang hủy..." : "Hủy đặt bàn"}
                      </button>
                    ) : null}
                  </div>
                </article>
              );
            })}
          </section>
        ) : null}
      </main>
    </div>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="reservation-history-info">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
