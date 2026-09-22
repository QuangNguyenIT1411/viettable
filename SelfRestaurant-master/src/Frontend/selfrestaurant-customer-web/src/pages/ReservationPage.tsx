import { FormEvent, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { PublicNavbar } from "../components/PublicNavbar";
import { api } from "../lib/api";
import type { CreateReservationPayload, ReservationDto } from "../lib/types";

type ReservationFormState = {
  customerName: string;
  phoneNumber: string;
  partySize: string;
  branchId: string;
  reservedAt: string;
  note: string;
};

const initialForm: ReservationFormState = {
  customerName: "",
  phoneNumber: "",
  partySize: "2",
  branchId: "",
  reservedAt: "",
  note: "",
};

const phoneNumberPattern = /^\d{10}$/;

function sanitizePhoneNumber(value: string) {
  return value.replace(/\D/g, "").slice(0, 10);
}

const statusLabels: Record<string, string> = {
  Pending: "Chờ xác nhận",
  Confirmed: "Đã xác nhận",
  CheckingIn: "Đang check-in",
  CheckedIn: "Đã check-in",
  Cancelled: "Đã hủy",
  NoShow: "Không đến",
  Completed: "Hoàn tất",
};

function toDateTimeLocalValue(date: Date) {
  const offsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}

function createIdempotencyKey() {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return `reservation-${crypto.randomUUID()}`;
  }

  return `reservation-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function formatDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat("vi-VN", {
    dateStyle: "full",
    timeStyle: "short",
  }).format(date);
}

function mapStatus(status: string) {
  return statusLabels[status] ?? status;
}

export function ReservationPage() {
  const session = useQuery({ queryKey: ["session"], queryFn: api.getSession });
  const branches = useQuery({ queryKey: ["branches"], queryFn: api.getBranches });
  const [form, setForm] = useState<ReservationFormState>(() => ({
    ...initialForm,
    customerName: session.data?.customer?.name ?? "",
    phoneNumber: session.data?.customer?.phoneNumber ?? "",
  }));
  const [validationError, setValidationError] = useState<string | null>(null);
  const [reservation, setReservation] = useState<ReservationDto | null>(null);

  const minReservationTime = useMemo(() => toDateTimeLocalValue(new Date()), []);
  const selectedBranch = branches.data?.find((branch) => branch.branchId === Number(form.branchId));

  useEffect(() => {
    const customer = session.data?.customer;
    if (!customer) return;

    setForm((current) => ({
      ...current,
      customerName: current.customerName || customer.name,
      phoneNumber: current.phoneNumber || customer.phoneNumber,
    }));
  }, [session.data?.customer]);

  const createReservation = useMutation({
    mutationFn: (payload: CreateReservationPayload) => api.createReservation(payload),
    onSuccess: (result) => {
      setReservation(result);
      setValidationError(null);
    },
    onError: (error) => {
      setValidationError(error instanceof Error ? error.message : "Không thể tạo đặt bàn. Vui lòng thử lại.");
    },
  });

  const cancelReservation = useMutation({
    mutationFn: (reservationId: number) => api.cancelReservation(reservationId),
    onSuccess: (result) => {
      setReservation(result);
      setValidationError(null);
    },
    onError: (error) => {
      setValidationError(error instanceof Error ? error.message : "Không thể hủy đặt bàn. Vui lòng thử lại.");
    },
  });

  function updateField<K extends keyof ReservationFormState>(field: K, value: ReservationFormState[K]) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function updatePhoneNumber(value: string) {
    updateField("phoneNumber", sanitizePhoneNumber(value));
  }

  function validateForm() {
    const customerName = form.customerName.trim();
    const phoneNumber = form.phoneNumber.trim();
    const partySize = Number(form.partySize);
    const branchId = Number(form.branchId);
    const reservedAt = form.reservedAt ? new Date(form.reservedAt) : null;

    if (!customerName) return "Vui lòng nhập họ tên khách hàng.";
    if (!phoneNumber) return "Vui lòng nhập số điện thoại.";
    if (!phoneNumberPattern.test(phoneNumber)) return "Số điện thoại phải gồm đúng 10 chữ số.";
    if (!Number.isFinite(partySize) || partySize < 1 || partySize > 30) return "Số khách phải từ 1 đến 30 người.";
    if (!Number.isFinite(branchId) || branchId <= 0) return "Vui lòng chọn chi nhánh.";
    if (!reservedAt || Number.isNaN(reservedAt.getTime())) return "Vui lòng chọn thời gian đặt bàn.";
    if (reservedAt.getTime() < Date.now() - 60_000) return "Thời gian đặt bàn không được ở quá khứ.";
    return null;
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const error = validateForm();
    if (error) {
      setValidationError(error);
      return;
    }

    createReservation.mutate({
      customerName: form.customerName.trim(),
      phoneNumber: form.phoneNumber.trim(),
      customerId: session.data?.customer?.customerId ?? null,
      branchId: Number(form.branchId),
      tableId: null,
      partySize: Number(form.partySize),
      reservedAt: new Date(form.reservedAt).toISOString(),
      note: form.note.trim() || null,
      idempotencyKey: createIdempotencyKey(),
    });
  }

  const canCancel = reservation && !["CheckedIn", "Completed", "Cancelled"].includes(reservation.status);

  return (
    <div className="reservation-page">
      <PublicNavbar />

      <header className="reservation-hero">
        <div className="home-container reservation-hero-grid">
          <div>
            <span className="reservation-kicker">Đặt bàn và đặt món trước</span>
            <h1>Đặt bàn trước</h1>
            <p>Giữ chỗ nhanh tại chi nhánh bạn yêu thích. Khi đến nhà hàng, nhân viên sẽ hỗ trợ check-in và phục vụ.</p>
          </div>
          <div className="reservation-hero-card">
            <i className="fas fa-calendar-check" />
            <strong>Không ảnh hưởng đặt món tại bàn</strong>
            <span>Quy trình QR hiện tại vẫn hoạt động như cũ.</span>
          </div>
        </div>
      </header>

      <main className="home-container reservation-content">
        {validationError ? (
          <div className="reservation-alert reservation-alert-error" role="alert">
            <i className="fas fa-circle-exclamation" />
            <span>{validationError}</span>
          </div>
        ) : null}

        {reservation ? (
          <section className="reservation-card reservation-success-card">
            <div className="reservation-success-icon">
              <i className="fas fa-check" />
            </div>
            <div className="reservation-success-copy">
              <span className="reservation-kicker">Đặt bàn thành công</span>
              <h2>Mã đặt bàn: {reservation.reservationCode}</h2>
              <p>Bạn có thể chọn món trước để được phục vụ nhanh hơn.</p>
            </div>

            <div className="reservation-detail-grid">
              <Detail label="Họ tên" value={reservation.customerName} />
              <Detail label="Số điện thoại" value={reservation.phoneNumber} />
              <Detail label="Số lượng khách" value={`${reservation.partySize} khách`} />
              <Detail label="Chi nhánh" value={selectedBranch?.name ?? `Chi nhánh #${reservation.branchId}`} />
              <Detail label="Thời gian đặt bàn" value={formatDateTime(reservation.reservedAt)} />
              <Detail label="Trạng thái" value={mapStatus(reservation.status)} />
              <Detail label="Ghi chú" value={reservation.note || "Không có"} wide />
            </div>

            {reservation.partySize > 4 ? (
              <p className="reservation-large-group-note"><i className="fas fa-users me-2" />Nhà hàng sẽ sắp xếp bàn phù hợp khi bạn đến.</p>
            ) : null}

            <div className="reservation-actions">
              <Link to={`/Reservation/${encodeURIComponent(reservation.reservationCode)}/PreOrder`} className="btn btn-outline-danger">
                <i className="fas fa-utensils me-2" />
                Chọn món trước
              </Link>
              <Link to="/Home/Index" className="btn btn-danger">
                <i className="fas fa-home me-2" />
                Về trang chủ
              </Link>
              {canCancel ? (
                <button
                  type="button"
                  className="btn btn-light reservation-cancel-btn"
                  disabled={cancelReservation.isPending}
                  onClick={() => cancelReservation.mutate(reservation.reservationId)}
                >
                  <i className="fas fa-ban me-2" />
                  {cancelReservation.isPending ? "Đang hủy..." : "Hủy đặt bàn"}
                </button>
              ) : null}
            </div>
            <p className="reservation-next-note">Món đặt trước chỉ được lưu nháp, nhà hàng sẽ chuẩn bị sau khi bạn check-in.</p>
          </section>
        ) : (
          <section className="reservation-form-shell">
            <div className="reservation-card reservation-info-card">
              <span className="reservation-kicker">Thông tin đặt bàn</span>
              <h2>Giữ chỗ trước khi đến</h2>
              <p>Nhập thông tin liên hệ và thời gian dự kiến. Nhà hàng sẽ dùng mã đặt bàn để hỗ trợ bạn khi đến.</p>
              <ul>
                <li>Không tạo đơn bếp ở bước này.</li>
                <li>Không giữ bàn khỏi quy trình QR hiện tại.</li>
                <li>Có thể hủy nếu chưa check-in.</li>
              </ul>
            </div>

            <form className="reservation-card reservation-form" onSubmit={handleSubmit} noValidate>
              <div className="reservation-form-grid">
                <label>
                  <span>Họ tên khách hàng</span>
                  <input
                    type="text"
                    className="form-control"
                    value={form.customerName}
                    onChange={(event) => updateField("customerName", event.target.value)}
                    placeholder="Nguyễn Văn A"
                    required
                  />
                </label>

                <label>
                  <span>Số điện thoại</span>
                  <input
                    type="tel"
                    className="form-control"
                    value={form.phoneNumber}
                    onChange={(event) => updatePhoneNumber(event.target.value)}
                    placeholder="0901234567"
                    inputMode="numeric"
                    pattern="\d{10}"
                    maxLength={10}
                    required
                  />
                </label>

                <label>
                  <span>Số lượng khách</span>
                  <input
                    type="number"
                    className="form-control"
                    min={1}
                    max={30}
                    value={form.partySize}
                    onChange={(event) => updateField("partySize", event.target.value)}
                    required
                  />
                  {Number(form.partySize) > 4 ? (
                    <small className="reservation-field-helper">Nhóm đông người có thể được nhà hàng sắp xếp nhiều bàn gần nhau.</small>
                  ) : null}
                </label>

                <label>
                  <span>Chi nhánh</span>
                  <select
                    className="form-select"
                    value={form.branchId}
                    onChange={(event) => updateField("branchId", event.target.value)}
                    disabled={branches.isLoading}
                    required
                  >
                    <option value="">{branches.isLoading ? "Đang tải chi nhánh..." : "Chọn chi nhánh"}</option>
                    {branches.data?.map((branch) => (
                      <option key={branch.branchId} value={branch.branchId}>
                        {branch.name}{branch.location ? ` - ${branch.location}` : ""}
                      </option>
                    ))}
                  </select>
                </label>

                <label>
                  <span>Thời gian đặt bàn</span>
                  <input
                    type="datetime-local"
                    className="form-control"
                    min={minReservationTime}
                    value={form.reservedAt}
                    onChange={(event) => updateField("reservedAt", event.target.value)}
                    required
                  />
                </label>

                <label className="reservation-note-field">
                  <span>Ghi chú</span>
                  <textarea
                    className="form-control"
                    rows={4}
                    value={form.note}
                    onChange={(event) => updateField("note", event.target.value)}
                    placeholder="Ví dụ: cần ghế trẻ em, sinh nhật, vị trí yên tĩnh..."
                  />
                </label>
              </div>

              <button type="submit" className="btn btn-danger btn-lg reservation-submit" disabled={createReservation.isPending || branches.isLoading}>
                <i className="fas fa-calendar-plus me-2" />
                {createReservation.isPending ? "Đang tạo đặt bàn..." : "Tạo đặt bàn"}
              </button>
            </form>
          </section>
        )}
      </main>
    </div>
  );
}

function Detail({ label, value, wide = false }: { label: string; value: string; wide?: boolean }) {
  return (
    <div className={`reservation-detail${wide ? " reservation-detail-wide" : ""}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
