import { userMessage } from "../../../../shared/apiError";
import { useEffect, useState } from "react";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi, type SupportQuickReplyDto } from "../../lib/api";
import type { StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = { onLogout: () => Promise<void> };
type Draft = { title: string; body: string; sortOrder: string; isActive: boolean };
const empty: Draft = { title: "", body: "", sortOrder: "0", isActive: true };

/**
 * "Hỗ trợ khách" (owner 06/11): support hours of the admin's branch (used by the chat's automatic reply; empty =
 * the branch's opening hours) and the ready answers the cashier picks from in the "Hỗ trợ" tab.
 */
export function SupportSettingsPage({ onLogout }: Props) {
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [openFrom, setOpenFrom] = useState("");
  const [openTo, setOpenTo] = useState("");
  const [branchHours, setBranchHours] = useState<string | null>(null);
  const [replies, setReplies] = useState<SupportQuickReplyDto[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draft, setDraft] = useState<Draft>(empty);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const [dashboard, hours, list] = await Promise.all([adminApi.getDashboard(), adminApi.getSupportHours(), adminApi.getSupportQuickReplies()]);
      setStaff(dashboard.staff);
      setOpenFrom(hours.hours?.openFrom ?? "");
      setOpenTo(hours.hours?.openTo ?? "");
      setBranchHours(hours.branchOpeningHours ?? null);
      setReplies(list.items);
    } catch (err) { setError(userMessage(err, "Không thể tải cài đặt hỗ trợ.")); }
    finally { setLoading(false); }
  }

  useEffect(() => { void loadPage(); }, []);

  async function saveHours() {
    setBusy(true); setError(null);
    try {
      const saved = await adminApi.saveSupportHours(openFrom.trim(), openTo.trim());
      setMessage(saved.openFrom ? `Đã lưu giờ hỗ trợ ${saved.openFrom}–${saved.openTo}.` : "Đã bỏ giờ hỗ trợ riêng, dùng giờ mở cửa của chi nhánh.");
    } catch (err) { setError(userMessage(err, "Không lưu được giờ hỗ trợ.")); }
    finally { setBusy(false); }
  }

  async function saveReply() {
    setBusy(true); setError(null);
    const body = { title: draft.title.trim(), body: draft.body.trim(), sortOrder: Number(draft.sortOrder) || 0, isActive: draft.isActive };
    try {
      if (editingId == null) await adminApi.createSupportQuickReply(body); else await adminApi.updateSupportQuickReply(editingId, body);
      setMessage(editingId == null ? "Đã thêm câu trả lời mẫu." : "Đã lưu câu trả lời mẫu.");
      setEditingId(null); setDraft(empty);
      setReplies((await adminApi.getSupportQuickReplies()).items);
    } catch (err) { setError(userMessage(err, "Không lưu được câu trả lời mẫu.")); }
    finally { setBusy(false); }
  }

  async function removeReply(id: number) {
    if (!window.confirm("Xoá câu trả lời mẫu này?")) return;
    setBusy(true);
    try { await adminApi.deleteSupportQuickReply(id); setReplies(replies.filter(r => r.supportQuickReplyId !== id)); setMessage("Đã xoá."); }
    catch (err) { setError(userMessage(err, "Không xoá được.")); }
    finally { setBusy(false); }
  }

  return (
    <AdminLayout title="Hỗ trợ khách" description="Giờ hỗ trợ dùng cho tin nhắn tự động trong chat; câu trả lời mẫu cho thu ngân chọn nhanh."
      staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải...</div> : <>
        <section className="panel">
          <h3>Giờ hỗ trợ</h3>
          <p className="muted">Khách nhắn ngoài giờ này sẽ nhận tin tự động báo giờ chi nhánh mở cửa. Để trống cả hai ô thì dùng giờ mở cửa của
            chi nhánh{branchHours ? ` (đang lưu: ${branchHours})` : " (chi nhánh chưa nhập giờ mở cửa: tin tự động sẽ không nêu giờ)"}.</p>
          <div className="form-row" style={{ display: "flex", gap: 12, alignItems: "end", flexWrap: "wrap" }}>
            <label>Từ<input type="time" value={openFrom} onChange={e => setOpenFrom(e.target.value)} aria-label="Giờ bắt đầu hỗ trợ" /></label>
            <label>Đến<input type="time" value={openTo} onChange={e => setOpenTo(e.target.value)} aria-label="Giờ kết thúc hỗ trợ" /></label>
            <button className="primary" disabled={busy} onClick={() => void saveHours()}>Lưu giờ hỗ trợ</button>
          </div>
        </section>

        <section className="panel">
          <h3>Câu trả lời mẫu</h3>
          <table className="data-table">
            <thead><tr><th>Tiêu đề</th><th>Nội dung</th><th>Thứ tự</th><th>Đang dùng</th><th>Thao tác</th></tr></thead>
            <tbody>
              {replies.length === 0 ? <tr><td colSpan={5} className="muted">Chưa có câu trả lời mẫu.</td></tr> : replies.map(r => (
                <tr key={r.supportQuickReplyId}>
                  <td><strong>{r.title}</strong></td>
                  <td style={{ whiteSpace: "pre-wrap" }}>{r.body}</td>
                  <td>{r.sortOrder}</td>
                  <td>{r.isActive ? "Có" : "Không"}</td>
                  <td>
                    <button className="ghost" onClick={() => { setEditingId(r.supportQuickReplyId); setDraft({ title: r.title, body: r.body, sortOrder: String(r.sortOrder), isActive: r.isActive }); }}>Sửa</button>{" "}
                    <button className="ghost" disabled={busy} onClick={() => void removeReply(r.supportQuickReplyId)}>Xoá</button>
                  </td>
                </tr>))}
            </tbody>
          </table>
          <div className="panel" style={{ marginTop: 12 }}>
            <strong>{editingId == null ? "Thêm câu trả lời mẫu" : "Sửa câu trả lời mẫu"}</strong>
            <div style={{ display: "grid", gap: 8, marginTop: 8 }}>
              <input value={draft.title} maxLength={100} placeholder="Tiêu đề (ví dụ: Đã nhận cọc)" aria-label="Tiêu đề" onChange={e => setDraft({ ...draft, title: e.target.value })} />
              <textarea value={draft.body} maxLength={1000} rows={3} placeholder="Nội dung gửi cho khách" aria-label="Nội dung" onChange={e => setDraft({ ...draft, body: e.target.value })} />
              <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
                <label>Thứ tự <input inputMode="numeric" value={draft.sortOrder} style={{ maxWidth: 70 }} onChange={e => setDraft({ ...draft, sortOrder: e.target.value })} /></label>
                <label><input type="checkbox" checked={draft.isActive} onChange={e => setDraft({ ...draft, isActive: e.target.checked })} /> Đang dùng</label>
                <button className="primary" disabled={busy || !draft.title.trim() || !draft.body.trim()} onClick={() => void saveReply()}>{editingId == null ? "Thêm" : "Lưu"}</button>
                {editingId != null ? <button className="ghost" onClick={() => { setEditingId(null); setDraft(empty); }}>Huỷ sửa</button> : null}
              </div>
            </div>
          </div>
        </section>
      </>}
    </AdminLayout>
  );
}
