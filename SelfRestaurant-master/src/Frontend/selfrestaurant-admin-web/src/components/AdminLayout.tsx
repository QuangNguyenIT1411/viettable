import { cleanText } from "../../../shared/apiError";
import { formatViDateTime } from "../../../shared/dateFormat";
import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import type { StaffSessionUserDto } from "../lib/types";

type Props = {
  title: string;
  description: string;
  staff?: StaffSessionUserDto | null;
  onLogout: () => Promise<void>;
  onRefresh?: (() => void | Promise<void>) | null;
  children: ReactNode;
  message?: string | null;
  error?: string | null;
};

const navSections = [
  {
    items: [
      { label: "T\u1ed5ng quan", icon: "bi-grid-fill", path: "/Admin/Dashboard/Index", match: "/admin/dashboard" },
      { label: "Danh m\u1ee5c", icon: "bi-folder2-open", path: "/Admin/Categories/Index", match: "/admin/categories" },
      { label: "Nguy\u00ean li\u1ec7u", icon: "bi-basket3-fill", path: "/Admin/Ingredients/Index", match: "/admin/ingredients" },
      { label: "Qu\u1ea3n l\u00fd kho", icon: "bi-box-seam", path: "/Admin/Inventory/Index", match: "/admin/inventory" },
      { label: "M\u00f3n \u0103n", icon: "bi-egg-fried", path: "/Admin/Dishes/Index", match: "/admin/dishes" },
      { label: "B\u00e0n & QR", icon: "bi-grid-3x3-gap-fill", path: "/Admin/TablesQR/Index", match: "/admin/tablesqr" },
      { label: "Nh\u00e2n vi\u00ean", icon: "bi-people-fill", path: "/Admin/Employees/Index", match: "/admin/employees" },
      { label: "Kh\u00e1ch h\u00e0ng", icon: "bi-person-badge-fill", path: "/Admin/Customers/Index", match: "/admin/customers" },
    ],
  },
  {
    title: "B\u00e1o c\u00e1o",
    items: [
      { label: "B\u00e1o c\u00e1o", icon: "bi-graph-up-arrow", path: "/Admin/Reports/Revenue", match: "/admin/reports" },
    ],
  },
  {
    title: "T\u00e0i kho\u1ea3n",
    items: [
      { label: "C\u00e0i \u0111\u1eb7t", icon: "bi-gear-fill", path: "/Admin/Settings/Index", match: "/admin/settings" },
    ],
  },
];

const categorySubItems = [
  { label: "Qu\u1ea3n l\u00fd \u0111\u01a1n v\u1ecb", icon: "bi-rulers", path: "/Admin/Units/Index", match: "/admin/units" },
  { label: "Qu\u1ea3n l\u00fd danh m\u1ee5c", icon: "bi-folder2-open", path: "/Admin/Categories/Index", match: "/admin/categories/index" },
  { label: "Qu\u1ea3n l\u00fd tr\u1ea1ng th\u00e1i", icon: "bi-tags", path: "/Admin/Categories/Statuses", match: "/admin/categories/statuses" },
];

const inventorySubItems = [
  { label: "T\u1ed5ng quan kho", icon: "bi-speedometer2", path: "/Admin/Inventory/Index", match: "/admin/inventory/index" },
  { label: "Nh\u1eadp kho", icon: "bi-box-arrow-in-down", path: "/Admin/Inventory/StockIn", match: "/admin/inventory/stockin" },
  { label: "Xu\u1ea5t kho", icon: "bi-box-arrow-up", path: "/Admin/Inventory/StockOut", match: "/admin/inventory/stockout" },
  { label: "L\u00f4 & h\u1ea1n s\u1eed d\u1ee5ng", icon: "bi-calendar2-week", path: "/Admin/Inventory/Batches", match: "/admin/inventory/batches" },
  { label: "L\u1ecbch s\u1eed xu\u1ea5t nh\u1eadp", icon: "bi-clock-history", path: "/Admin/Inventory/Movements", match: "/admin/inventory/movements" },
];

export function AdminLayout({ title, description, staff, onLogout, onRefresh, children, message, error }: Props) {
  const location = useLocation();
  const navigate = useNavigate();
  const normalizedPath = location.pathname.toLowerCase();
  const categoryGroupActive = normalizedPath.startsWith("/admin/categories") || normalizedPath.startsWith("/admin/units");
  const inventoryGroupActive = normalizedPath.startsWith("/admin/inventory");
  const [categoriesOpen, setCategoriesOpen] = useState(categoryGroupActive);
  const [inventoryOpen, setInventoryOpen] = useState(inventoryGroupActive);

  useEffect(() => {
    if (categoryGroupActive) setCategoriesOpen(true);
  }, [categoryGroupActive]);

  useEffect(() => {
    if (inventoryGroupActive) setInventoryOpen(true);
  }, [inventoryGroupActive]);

  return (
    <main className="admin-shell">
      <div className="admin-layout">
        <aside className="admin-sidebar">
          <div className="sidebar-card">
            <div className="sidebar-header">
              <span className="vt-brand-mark" aria-hidden="true">VT</span>
              <div>
                <div className="sidebar-title">VietTable</div>
                <div className="sidebar-subtitle">{"Qu\u1ea3n tr\u1ecb"}{staff?.name ? ` \u00b7 ${staff.name}` : ""}</div>
              </div>
            </div>
            <div className="sidebar-links">
              {navSections.map((section) => (
                <div key={section.title ?? "main"} className="sidebar-group">
                  {section.title ? <div className="sidebar-group-title">{section.title}</div> : null}
                  {section.items.map((item) => {
                    if (item.match === "/admin/categories") {
                      return (
                        <div key={item.path} className="sidebar-menu-group">
                          <button
                            className={`sidebar-link sidebar-parent ${categoryGroupActive ? "active" : ""}`}
                            onClick={() => setCategoriesOpen((current) => !current)}
                            aria-expanded={categoriesOpen}
                            aria-controls="admin-category-submenu"
                          >
                            <i className={`bi ${item.icon}`} />
                            <span>{item.label}</span>
                            <i className={`bi ${categoriesOpen ? "bi-chevron-up" : "bi-chevron-down"} sidebar-chevron`} />
                          </button>
                          {categoriesOpen ? (
                            <div id="admin-category-submenu" className="sidebar-submenu">
                              {categorySubItems.map((subItem) => (
                                <button
                                  key={subItem.path}
                                  className={`sidebar-link sidebar-sub-link ${normalizedPath.startsWith(subItem.match) ? "active" : ""}`}
                                  onClick={() => navigate(subItem.path)}
                                >
                                  <i className={`bi ${subItem.icon}`} />
                                  <span>{subItem.label}</span>
                                </button>
                              ))}
                            </div>
                          ) : null}
                        </div>
                      );
                    }

                    if (item.match === "/admin/inventory") {
                      return (
                        <div key={item.path} className="sidebar-menu-group">
                          <button
                            className={`sidebar-link sidebar-parent ${inventoryGroupActive ? "active" : ""}`}
                            onClick={() => setInventoryOpen((current) => !current)}
                            aria-expanded={inventoryOpen}
                            aria-controls="admin-inventory-submenu"
                          >
                            <i className={`bi ${item.icon}`} />
                            <span>{item.label}</span>
                            <i className={`bi ${inventoryOpen ? "bi-chevron-up" : "bi-chevron-down"} sidebar-chevron`} />
                          </button>
                          {inventoryOpen ? (
                            <div id="admin-inventory-submenu" className="sidebar-submenu">
                              {inventorySubItems.map((subItem) => (
                                <button
                                  key={subItem.path}
                                  className={`sidebar-link sidebar-sub-link ${normalizedPath.startsWith(subItem.match) ? "active" : ""}`}
                                  onClick={() => navigate(subItem.path)}
                                >
                                  <i className={`bi ${subItem.icon}`} />
                                  <span>{subItem.label}</span>
                                </button>
                              ))}
                            </div>
                          ) : null}
                        </div>
                      );
                    }

                    return (
                      <button
                        key={item.path}
                        className={`sidebar-link ${normalizedPath.startsWith(item.match) ? "active" : ""}`}
                        onClick={() => navigate(item.path)}
                      >
                        <i className={`bi ${item.icon}`} />
                        <span>{item.label}</span>
                      </button>
                    );
                  })}
                </div>
              ))}
              <button className="sidebar-link" onClick={() => void onLogout()}>
                <i className="bi bi-box-arrow-right" />
                <span>{"\u0110\u0103ng xu\u1ea5t"}</span>
              </button>
            </div>
          </div>
        </aside>

        <section className="admin-main">
          <header className="admin-page-header">
            <div>
              <h1>{title}</h1>
              <p className="muted-line">
                {description}
                <span className="admin-page-context">
                  <i className="bi bi-building" aria-hidden="true" /> {staff?.branchName ?? "Ch\u01b0a c\u00f3 chi nh\u00e1nh"}
                  {" \u00b7 "}
                  <i className="bi bi-shield-check" aria-hidden="true" /> {staff?.roleName ?? "Qu\u1ea3n tr\u1ecb"}
                </span>
              </p>
            </div>
            <div className="header-actions">
              <span className="admin-clock">{formatViDateTime(new Date())}</span>
              {onRefresh ? (
                <button className="ghost" onClick={() => void onRefresh()}>
                  <i className="bi bi-arrow-clockwise" aria-hidden="true" /> {"L\u00e0m m\u1edbi"}
                </button>
              ) : null}
            </div>
          </header>

          {message ? <div className="success-box">{message}</div> : null}
          {cleanText(error) ? <div className="error-box">{cleanText(error)}</div> : null}

          {children}
        </section>
      </div>
    </main>
  );
}
