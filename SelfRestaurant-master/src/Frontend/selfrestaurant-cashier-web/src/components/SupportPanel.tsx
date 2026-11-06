import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { cleanText, userMessage } from "../../../shared/apiError";
import { formatViDateTime } from "../../../shared/dateFormat";
import type { TopicBus } from "../../../shared/staffRealtime";
import { actionHash, seenByStaff, senderLabel, supportStatusLabel, supportStatusTone, totalUnread, waitingText } from "../../../shared/supportChat";
import { cashierApi, type SupportConversation, type SupportDetail, type SupportMessage, type SupportQuickReply } from "../lib/api";
import { CASHIER_FALLBACK_POLL_MS, CASHIER_OFFLINE_POLL_MS } from "../lib/polling";
import { useControlledRead } from "../lib/useControlledRead";
import { parseApiUtcTimestamp } from "../lib/dateTime";

function when(value: string | null | undefined): string {
  const date = value ? parseApiUtcTimestamp(value) : null;
  return date ? formatViDateTime(date) : "";
}

type Props = { events?: TopicBus; live?: boolean; onUnreadChange?: (count: number) => void; onOpenTab?: (hash: string) => void };

/**
 * Cashier "Hỗ trợ" tab (owner 06/11): the branch's support conversations, the one waiting longest on top; the open one
 * shows its topic, the attached item with its live status and a link to where it is handled, the messages, ready
 * answers and "Đã giải quyết". Customer messages arrive by the "support" staff event (slow poll as a fallback).
 */
export function SupportPanel({ events, live = false, onUnreadChange, onOpenTab }: Props) {
  const [items, setItems] = useState<SupportConversation[]>([]);
  const [showResolved, setShowResolved] = useState(false);
  const [openId, setOpenId] = useState<number | null>(null);
  const [detail, setDetail] = useState<SupportDetail | null>(null);
  const [messages, setMessages] = useState<SupportMessage[]>([]);
  const [hasOlder, setHasOlder] = useState(false);
  const [quickReplies, setQuickReplies] = useState<SupportQuickReply[]>([]);
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const listRef = useRef<HTMLDivElement>(null);

  const loadList = useControlledRead({
    read: signal => cashierApi.getSupportConversations(showResolved, signal),
    apply: value => { setItems(value.items); setError(""); },
    error: e => setError(userMessage(e, "Không tải được danh sách hỗ trợ.")),
    interval: live ? CASHIER_FALLBACK_POLL_MS : CASHIER_OFFLINE_POLL_MS,
  });

  const openConversation = useCallback(async (id: number, keepScroll = false) => {
    try {
      const value = await cashierApi.getSupportConversation(id);
      setDetail(value);
      setMessages(value.messages);
      setHasOlder(value.hasOlder);
      const newest = value.messages[value.messages.length - 1];
      if (newest && value.conversation.unread > 0) await cashierApi.readSupport(id, newest.supportMessageId);
      if (!keepScroll) requestAnimationFrame(() => listRef.current?.scrollTo({ top: listRef.current.scrollHeight }));
    } catch (e) { setError(userMessage(e, "Không mở được cuộc trò chuyện.")); }
  }, []);

  useEffect(() => { void loadList(); }, [showResolved, loadList]);
  useEffect(() => events?.on("support", () => {
    void loadList();
    if (openId != null) void openConversation(openId);
  }), [events, loadList, openConversation, openId]);
  useEffect(() => { onUnreadChange?.(totalUnread(items)); }, [items, onUnreadChange]);
  useEffect(() => {
    cashierApi.getSupportQuickReplies().then(v => setQuickReplies(v.items)).catch(() => setQuickReplies([]));
  }, []);

  async function loadOlder() {
    if (openId == null || messages.length === 0) return;
    const el = listRef.current;
    const before = el ? el.scrollHeight - el.scrollTop : 0;
    try {
      const value = await cashierApi.getSupportConversation(openId, messages[0].supportMessageId);
      setMessages([...value.messages, ...messages]);
      setHasOlder(value.hasOlder);
      requestAnimationFrame(() => { if (el) el.scrollTop = el.scrollHeight - before; });
    } catch (e) { setError(userMessage(e, "Không tải được tin cũ.")); }
  }

  async function send() {
    const body = draft.trim();
    if (!body || openId == null) return;
    setBusy(true);
    try {
      await cashierApi.replySupport(openId, body);
      setDraft("");
      await openConversation(openId);
      await loadList();
    } catch (e) { setError(userMessage(e, "Không gửi được tin nhắn.")); }
    finally { setBusy(false); }
  }

  async function resolve() {
    if (openId == null) return;
    setBusy(true);
    try { await cashierApi.resolveSupport(openId); await openConversation(openId, true); await loadList(); }
    catch (e) { setError(userMessage(e, "Không đánh dấu được.")); }
    finally { setBusy(false); }
  }

  const conversation = detail?.conversation;
  const link = useMemo(() => actionHash(detail?.context?.actionTab), [detail]);

  return <section className="panel cashier-panel-card" aria-label="Hỗ trợ khách hàng">
    <div className="cashier-panel-header">
      <div><h2><i className="bi bi-chat-dots me-2" />Hỗ trợ khách hàng</h2>
        <p className="muted">Khách nhắn từ app cho chi nhánh này. Cuộc chờ lâu nhất ở trên cùng.</p></div>
      <div className="support-header-actions">
        <label className="muted"><input type="checkbox" checked={showResolved} onChange={e => setShowResolved(e.target.checked)} /> Hiện cả cuộc đã giải quyết (7 ngày)</label>
        <button type="button" className="ghost" onClick={() => void loadList()}>Làm mới</button>
      </div>
    </div>
    {cleanText(error) ? <div className="error-box" role="alert">{cleanText(error)}</div> : null}
    <div className="support-layout">
      <ul className="support-list" aria-label="Danh sách cuộc trò chuyện">
        {items.length === 0 ? <li className="muted support-empty">Chưa có khách nào cần hỗ trợ.</li> : items.map(c =>
          <li key={c.supportConversationId}>
            <button type="button" className={`support-item${c.supportConversationId === openId ? " selected" : ""}`}
              onClick={() => { setOpenId(c.supportConversationId); void openConversation(c.supportConversationId); }}>
              <span className="support-item-top">
                <strong>{c.customerName}</strong>
                {c.unread > 0 ? <span className="support-unread" aria-label={`${c.unread} tin chưa đọc`}>{c.unread}</span> : null}
              </span>
              <span className="support-topic">{c.topicLabel}</span>
              {c.lastMessage ? <span className="support-preview">{c.lastMessage.body}</span> : null}
              <span className="support-item-meta">
                <span className={`soft-badge ${supportStatusTone(c.status)}`}>{supportStatusLabel(c.status)}</span>
                {c.status !== "Resolved" && c.waitingSinceUtc ? <span className="muted">{waitingText(c.waitingSinceUtc)}</span> : null}
              </span>
            </button>
          </li>)}
      </ul>

      <div className="support-thread">
        {!conversation ? <p className="muted support-empty">Chọn một cuộc trò chuyện để xem và trả lời.</p> : <>
          <div className="support-thread-head">
            <div>
              <strong>{conversation.customerName}</strong> · <span>{conversation.topicLabel}</span>
              <div className="muted">Mở lúc {when(conversation.createdAtUtc)} · <span className={`soft-badge ${supportStatusTone(conversation.status)}`}>{supportStatusLabel(conversation.status)}</span>
                {conversation.resolvedByName ? <> · Giải quyết bởi {conversation.resolvedByName}</> : null}</div>
            </div>
            {conversation.status !== "Resolved"
              ? <button type="button" className="secondary" disabled={busy} onClick={() => void resolve()}><i className="bi bi-check2-circle me-1" />Đã giải quyết</button>
              : null}
          </div>
          {detail?.context ? <div className="support-context">
            <div><span className="muted">Đính kèm:</span> {detail.context.label}</div>
            <div><span className="muted">Trạng thái hiện tại:</span> <strong>{detail.context.status}</strong>
              {link && detail.context.actionLabel ? <> · <a href={link} onClick={e => { e.preventDefault(); onOpenTab?.(link); }}>{detail.context.actionLabel}</a></> : null}</div>
          </div> : conversation.contextLabel ? <div className="support-context muted">Đính kèm: {conversation.contextLabel}</div> : null}

          <div className="support-messages" ref={listRef} onScroll={e => { if (hasOlder && e.currentTarget.scrollTop < 40) void loadOlder(); }}>
            {hasOlder ? <button type="button" className="ghost support-older" onClick={() => void loadOlder()}>Xem tin cũ hơn</button> : null}
            {messages.map(m => <div key={m.supportMessageId} className={`support-bubble ${m.senderType.toLowerCase()}`}>
              <div className="support-bubble-who">{senderLabel(m.senderType, m.senderName, conversation.customerName)}</div>
              <div className="support-bubble-body">{m.body}</div>
              <div className="support-bubble-time">{when(m.createdAtUtc)}
                {m.senderType === "STAFF" ? (m.supportMessageId <= conversation.customerLastReadMessageId ? " · Khách đã xem" : " · Đã gửi") : null}
                {m.senderType === "CUSTOMER" && seenByStaff(m.supportMessageId, conversation.staffLastReadMessageId) ? " · Đã xem" : null}</div>
            </div>)}
          </div>

          {quickReplies.length > 0 ? <div className="support-quick" aria-label="Câu trả lời mẫu">
            {quickReplies.map(q => <button key={q.supportQuickReplyId} type="button" className="ghost" title={q.body}
              onClick={() => setDraft(draft ? `${draft}\n${q.body}` : q.body)}>{q.title}</button>)}
          </div> : null}
          <div className="support-compose">
            <textarea value={draft} maxLength={1000} rows={3} placeholder="Nhập câu trả lời cho khách…" aria-label="Câu trả lời"
              onChange={e => setDraft(e.target.value)}
              onKeyDown={e => { if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) void send(); }} />
            <button type="button" className="primary" disabled={busy || !draft.trim()} onClick={() => void send()}>Gửi</button>
          </div>
        </>}
      </div>
    </div>
  </section>;
}
