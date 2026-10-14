import { Link } from "react-router-dom";

type Props = {
  active: "pos" | "report" | "history";
  branchName?: string | null;
  staffName?: string | null;
  roleName?: string | null;
  onLogout?: () => void;
};

const links = [
  { key: "pos", to: "/Staff/Cashier/Index", icon: "bi-grid-1x2", label: "Quầy thu ngân" },
  { key: "report", to: "/Staff/Cashier/Report", icon: "bi-printer", label: "Báo cáo theo ngày" },
  { key: "history", to: "/Staff/Cashier/History", icon: "bi-clock-history", label: "Lịch sử & Tài khoản" },
] as const;

export function CashierTopbar({ active, branchName, staffName, roleName, onLogout }: Props) {
  return (
    <header className="vt-topbar">
      <div className="vt-brand">
        <span className="vt-brand-mark" aria-hidden="true">VT</span>
        <span>
          VietTable
          <small>Quầy thu ngân{branchName ? ` · ${branchName}` : ""}</small>
        </span>
      </div>
      <nav className="vt-nav" aria-label="Điều hướng thu ngân">
        {links.map((link) => (
          <Link key={link.key} to={link.to} aria-current={active === link.key ? "page" : undefined}>
            <i className={`bi ${link.icon}`} aria-hidden="true" />
            {link.label}
          </Link>
        ))}
      </nav>
      <div className="vt-spacer" />
      {staffName ? (
        <div className="vt-who">
          <b>{staffName}</b>
          {roleName}
        </div>
      ) : null}
      {onLogout ? (
        <button type="button" className="vt-btn vt-btn-secondary" onClick={onLogout}>
          <i className="bi bi-box-arrow-right" aria-hidden="true" />
          Đăng xuất
        </button>
      ) : null}
    </header>
  );
}
