import { useEffect, useRef, useState } from "react";
import { Link, NavLink, useLocation, useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../lib/api";
import { toMvcPath } from "../lib/mvcPaths";

const labels = {
  toggleNav: "Chuyn iu hng",
  home: "Trang Chủ",
  about: "Về Chúng Tôi",
  contact: "Liên Hệ",
  reservation: "Đặt bàn trước",
  reservationHistory: "Lịch sử đặt bàn",
  currentTable: "Bàn hiện tại",
  dashboard: "Dashboard",
  profile: "Hồ Sơ Cá Nhân",
  logout: "Đăng Xuất",
  login: "Đăng Nhập",
  register: "Đăng Ký",
} as const;

export function PublicNavbar() {
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const dropdownRef = useRef<HTMLLIElement | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const { data: session } = useQuery({ queryKey: ["session"], queryFn: api.getSession });

  const path = location.pathname.toLowerCase();
  const isHomeRoute = path === "/" || path === "/home" || path === "/home/index";
  const isAboutRoute = path === "/about" || path === "/home/about";
  const isContactRoute = path === "/contact" || path === "/home/contact";
  const isReservationRoute = path === "/reservation" || path === "/reservation/index";

  const logout = useMutation({
    mutationFn: api.logout,
    onSuccess: async (result) => {
      await queryClient.invalidateQueries();
      navigate(toMvcPath(result.nextPath));
    },
  });

  useEffect(() => {
    setMenuOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!menuOpen) return;

    function handlePointerDown(event: MouseEvent) {
      if (!dropdownRef.current?.contains(event.target as Node)) {
        setMenuOpen(false);
      }
    }

    function handleEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setMenuOpen(false);
      }
    }

    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("keydown", handleEscape);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("keydown", handleEscape);
    };
  }, [menuOpen]);

  return (
    <nav className="navbar navbar-expand-lg navbar-light bg-white shadow-sm sticky-top topbar">
      <div className="container">
        <Link to="/Home/Index" className="navbar-brand brand">
          Self Restaurant
        </Link>

        <button
          className="navbar-toggler"
          type="button"
          data-bs-toggle="collapse"
          data-bs-target="#customerNavbar"
          aria-controls="customerNavbar"
          aria-expanded="false"
          aria-label={labels.toggleNav}
        >
          <span className="navbar-toggler-icon" />
        </button>

        <div className="collapse navbar-collapse" id="customerNavbar">
          <ul className="navbar-nav ms-auto">
            <li className="nav-item">
              <NavLink to="/Home/Index" className={() => `nav-link${isHomeRoute ? " active" : ""}`}>
                {labels.home}
              </NavLink>
            </li>
            <li className="nav-item">
              <NavLink to="/Home/About" className={() => `nav-link${isAboutRoute ? " active" : ""}`}>
                {labels.about}
              </NavLink>
            </li>
            <li className="nav-item">
              <NavLink to="/Home/Contact" className={() => `nav-link${isContactRoute ? " active" : ""}`}>
                {labels.contact}
              </NavLink>
            </li>
            <li className="nav-item">
              <NavLink to="/Reservation/Index" className={() => `nav-link${isReservationRoute ? " active" : ""}`}>
                <i className="fas fa-calendar-check me-1" />
                {labels.reservation}
              </NavLink>
            </li>
            <li className="nav-item">
              <NavLink to="/Reservation/My" className="nav-link">
                <i className="fas fa-clock-rotate-left me-1" />
                {labels.reservationHistory}
              </NavLink>
            </li>

            {session?.customer ? (
              <>
                {session.tableContext ? (
                  <li className="nav-item me-2">
                    <Link to="/Menu/Index" className="nav-link btn btn-outline-danger px-3 text-danger">
                      <i className="fas fa-chair me-1" />
                      {labels.currentTable} ({session.tableContext.tableNumber ?? session.tableContext.tableId})
                    </Link>
                  </li>
                ) : null}

                <li ref={dropdownRef} className={`nav-item dropdown${menuOpen ? " show" : ""}`}>
                  <button
                    type="button"
                    className="nav-link dropdown-toggle customer-dropdown dropdown-toggle-btn"
                    id="customerDropdown"
                    aria-expanded={menuOpen}
                    aria-haspopup="true"
                    onClick={() => setMenuOpen((current) => !current)}
                  >
                    <i className="fas fa-user-circle me-1" />
                    {session.customer.name}
                  </button>

                  <ul className={`dropdown-menu dropdown-menu-end${menuOpen ? " show" : ""}`} aria-labelledby="customerDropdown">
                    <li>
                      <Link className="dropdown-item" to="/Customer/Dashboard" onClick={() => setMenuOpen(false)}>
                        <i className="fas fa-chart-line me-2" />
                        {labels.dashboard}
                      </Link>
                    </li>
                    <li>
                      <Link className="dropdown-item" to="/Customer/Profile" onClick={() => setMenuOpen(false)}>
                        <i className="fas fa-user me-2" />
                        {labels.profile}
                      </Link>
                    </li>
                    <li>
                      <hr className="dropdown-divider" />
                    </li>
                    <li>
                      <button
                        type="button"
                        className="dropdown-item text-danger"
                        onClick={() => {
                          setMenuOpen(false);
                          logout.mutate();
                        }}
                      >
                        <i className="fas fa-sign-out-alt me-2" />
                        {labels.logout}
                      </button>
                    </li>
                  </ul>
                </li>
              </>
            ) : (
              <>
                <li className="nav-item">
                  <NavLink to="/Customer/Login" className="nav-link">
                    <i className="fas fa-sign-in-alt me-1" />
                    {labels.login}
                  </NavLink>
                </li>
                <li className="nav-item">
                  <NavLink to="/Customer/Register" className="nav-link btn btn-danger btn-sm ms-2 text-white">
                    <i className="fas fa-user-plus me-1" />
                    {labels.register}
                  </NavLink>
                </li>
              </>
            )}
          </ul>
        </div>
      </div>
    </nav>
  );
}
