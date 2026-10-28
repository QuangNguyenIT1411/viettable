import { formatVnd, parseVnd } from "../../../shared/money";

type Props = { label: string; value: string; onChange: (value: string) => void; placeholder?: string };

/**
 * Money box for the admin forms (owner 29/10): plain text so "300.000" stays 300.000đ (a number box read it as 300),
 * with the amount it will save written under it.
 */
export function MoneyInput({ label, value, onChange, placeholder }: Props) {
  const amount = parseVnd(value);
  const hint = value.trim() === "" ? null : Number.isNaN(amount) ? "Chỉ nhập số, ví dụ 300000" : `= ${formatVnd(amount)}`;
  return (
    <label>{label}
      <input type="text" inputMode="numeric" value={value} placeholder={placeholder} onChange={e => onChange(e.target.value)} />
      {hint ? <small className="muted" style={Number.isNaN(amount) ? { color: "var(--color-danger)" } : undefined}>{hint}</small> : null}
    </label>
  );
}
