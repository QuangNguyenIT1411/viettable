import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PublicNavbar } from "../components/PublicNavbar";
import { api } from "../lib/api";
import type { MenuDishDto, ReservationDto } from "../lib/types";

type PreOrderCartItem = {
  dishId: number;
  dishNameSnapshot: string;
  unitPriceSnapshot: number;
  quantity: number;
  note: string;
};

type PreOrderDish = MenuDishDto & {
  categoryId: number;
  categoryName: string;
};

const closedStatuses = new Set(["Cancelled", "CheckedIn", "Completed", "NoShow"]);
const statusLabels: Record<string, string> = {
  Pending: "Chờ xác nhận",
  Confirmed: "Đã xác nhận",
  CheckingIn: "Đang check-in",
  CheckedIn: "Đã check-in",
  Cancelled: "Đã hủy",
  NoShow: "Không đến",
  Completed: "Hoàn tất",
};

const placeholderDishImage = "/images/placeholder-dish.svg";
const vnd = (value: number) => `${value.toLocaleString("vi-VN")} đ`;

function slugifyDishName(name: string) {
  return name
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

function resolveDishImage(image: string | null | undefined, dishName: string) {
  const normalized = (image ?? "").trim();
  if (normalized.startsWith("/images/") || normalized.startsWith("http://") || normalized.startsWith("https://") || normalized.startsWith("data:")) {
    return normalized;
  }

  if (normalized.startsWith("/")) {
    return normalized;
  }

  const slug = slugifyDishName(dishName);
  return slug ? `/images/${slug}.jpg` : placeholderDishImage;
}

function handleDishImageError(event: React.SyntheticEvent<HTMLImageElement>) {
  const img = event.currentTarget;
  if (!img.src.endsWith(placeholderDishImage)) {
    img.src = placeholderDishImage;
  }
}

function normalizeText(value: string | null | undefined) {
  return (value ?? "").trim();
}

function formatDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat("vi-VN", { dateStyle: "medium", timeStyle: "short" }).format(date);
}

function flattenAvailableDishes(reservation: ReservationDto | undefined, menuDishes: PreOrderDish[]) {
  return menuDishes
    .filter((dish) => dish.available)
    .map((dish) => ({
      dish,
      score: (dish.isDailySpecial ? 3 : 0) + (dish.isVegetarian ? 1 : 0) + Math.min(reservation?.partySize ?? 1, 8) * 0.1,
    }))
    .sort((left, right) => right.score - left.score)
    .slice(0, 6)
    .map((item) => item.dish);
}

export function ReservationPreOrderPage() {
  const { code = "" } = useParams();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | "all">("all");
  const [showAvailableOnly, setShowAvailableOnly] = useState(true);
  const [cartItems, setCartItems] = useState<PreOrderCartItem[]>([]);
  const [selectedDish, setSelectedDish] = useState<PreOrderDish | null>(null);
  const [modalQuantity, setModalQuantity] = useState(1);
  const [modalNote, setModalNote] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reservation = useQuery({
    queryKey: ["reservation", code],
    queryFn: () => api.getReservation(code),
    enabled: Boolean(code),
    retry: false,
  });

  const menu = useQuery({
    queryKey: ["reservationPreOrderMenu", code, reservation.data?.branchId],
    queryFn: () => api.getReservationMenu(code),
    enabled: Boolean(code && reservation.data?.branchId),
    retry: false,
  });

  useEffect(() => {
    if (!reservation.data) return;
    setCartItems(
      reservation.data.preOrderItems
        .filter((item) => item.status === "Pending")
        .map((item) => ({
          dishId: item.dishId,
          dishNameSnapshot: item.dishNameSnapshot,
          unitPriceSnapshot: item.unitPriceSnapshot,
          quantity: item.quantity,
          note: item.note ?? "",
        })),
    );
  }, [reservation.data]);

  const categories = useMemo(() => menu.data?.categories ?? [], [menu.data]);
  const allDishes = useMemo<PreOrderDish[]>(
    () =>
      categories.flatMap((category) =>
        category.dishes.map((dish) => ({
          ...dish,
          categoryId: category.categoryId,
          categoryName: category.categoryName,
        })),
      ),
    [categories],
  );
  const hasReservationBranch = !reservation.data || reservation.data.branchId > 0;
  const filteredDishes = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return allDishes.filter((dish) => {
      if (showAvailableOnly && !dish.available) return false;
      if (selectedCategoryId !== "all" && dish.categoryId !== selectedCategoryId) return false;
      if (!keyword) return true;
      return `${dish.name} ${dish.description ?? ""} ${dish.categoryName}`.toLowerCase().includes(keyword);
    });
  }, [allDishes, search, selectedCategoryId, showAvailableOnly]);
  const recommendedDishes = useMemo(() => flattenAvailableDishes(reservation.data, allDishes), [allDishes, reservation.data]);
  const subtotal = cartItems.reduce((sum, item) => sum + item.unitPriceSnapshot * item.quantity, 0);
  const totalQuantity = cartItems.reduce((sum, item) => sum + item.quantity, 0);
  const isClosed = reservation.data ? closedStatuses.has(reservation.data.status) : false;
  const activeFilters = search.trim() || selectedCategoryId !== "all" || !showAvailableOnly;

  const savePreOrder = useMutation({
    mutationFn: () => {
      if (!reservation.data) throw new Error("Không tìm thấy đặt bàn.");
      return api.replaceReservationPreOrderItems(reservation.data.reservationId, {
        items: cartItems.map((item) => ({
          dishId: item.dishId,
          dishNameSnapshot: item.dishNameSnapshot,
          unitPriceSnapshot: item.unitPriceSnapshot,
          quantity: item.quantity,
          note: item.note.trim() || null,
        })),
      });
    },
    onSuccess: async (result) => {
      setMessage("Đã lưu món đặt trước. Nhà hàng sẽ chuẩn bị sau khi bạn check-in.");
      setErrorMessage(null);
      await queryClient.setQueryData(["reservation", code], result);
    },
    onError: (error) => {
      setMessage(null);
      setErrorMessage(error instanceof Error ? error.message : "Không thể lưu món đặt trước. Vui lòng thử lại.");
    },
  });

  function addDish(dish: PreOrderDish, quantity = 1, note = "") {
    setMessage(null);
    setErrorMessage(null);
    setCartItems((current) => {
      const existing = current.find((item) => item.dishId === dish.dishId);
      if (existing) {
        return current.map((item) =>
          item.dishId === dish.dishId
            ? { ...item, quantity: item.quantity + quantity, note: note.trim() ? note : item.note }
            : item,
        );
      }

      return [
        ...current,
        {
          dishId: dish.dishId,
          dishNameSnapshot: dish.name,
          unitPriceSnapshot: dish.price,
          quantity,
          note,
        },
      ];
    });
  }

  function openDish(dish: PreOrderDish) {
    setSelectedDish(dish);
    setModalQuantity(1);
    setModalNote("");
  }

  function addSelectedDish() {
    if (!selectedDish || isClosed || !selectedDish.available) return;
    addDish(selectedDish, Math.max(1, modalQuantity), modalNote);
    setSelectedDish(null);
  }

  function updateQuantity(dishId: number, quantity: number) {
    const safeQuantity = Math.max(1, Number.isFinite(quantity) ? Math.floor(quantity) : 1);
    setCartItems((current) => current.map((item) => (item.dishId === dishId ? { ...item, quantity: safeQuantity } : item)));
  }

  function updateNote(dishId: number, note: string) {
    setCartItems((current) => current.map((item) => (item.dishId === dishId ? { ...item, note } : item)));
  }

  function removeItem(dishId: number) {
    setCartItems((current) => current.filter((item) => item.dishId !== dishId));
  }

  function resetFilters() {
    setSearch("");
    setSelectedCategoryId("all");
    setShowAvailableOnly(true);
  }

  function handleSave() {
    setMessage(null);
    setErrorMessage(null);
    savePreOrder.mutate();
  }

  return (
    <div className="reservation-page preorder-page">
      <PublicNavbar />
      <main className="home-container preorder-shell">
        <section className="preorder-hero">
          <div className="preorder-hero-copy">
            <span className="reservation-kicker"><i className="bi bi-stars" /> Đặt món trước</span>
            <h1>Chọn món trước</h1>
            <p>Danh sách này sẽ được gửi đến nhà hàng và chỉ tạo đơn sau khi bạn check-in.</p>
            <div className="preorder-hero-actions">
              <Link to="/Reservation/My" className="btn btn-outline-secondary">Lịch đặt bàn</Link>
              <a href="#preorder-menu" className="btn btn-danger">Xem thực đơn</a>
            </div>
          </div>

          {reservation.data ? (
            <div className="preorder-hero-card">
              <div className="preorder-hero-card-top">
                <span>Reservation</span>
                <strong>{reservation.data.reservationCode}</strong>
              </div>
              <div className="preorder-hero-card-grid">
                <Summary label="Khách hàng" value={reservation.data.customerName} />
                <Summary label="Điện thoại" value={reservation.data.phoneNumber} />
                <Summary label="Số khách" value={`${reservation.data.partySize} người`} />
                <Summary label="Thời gian" value={formatDateTime(reservation.data.reservedAt)} />
              </div>
              <div className="preorder-hero-card-status">
                <span className={`preorder-status preorder-status-${reservation.data.status.toLowerCase()}`}>
                  {statusLabels[reservation.data.status] ?? reservation.data.status}
                </span>
                <small>{menu.data?.branchName ?? `Chi nhánh #${reservation.data.branchId}`}</small>
              </div>
            </div>
          ) : null}
        </section>

        {message ? <div className="reservation-alert preorder-alert-success"><i className="fas fa-check-circle" />{message}</div> : null}
        {errorMessage ? <div className="reservation-alert reservation-alert-error"><i className="fas fa-triangle-exclamation" />{errorMessage}</div> : null}
        {reservation.error ? <div className="reservation-alert reservation-alert-error">Không thể tải thông tin đặt bàn.</div> : null}

        {reservation.isLoading ? <div className="reservation-card preorder-empty">Đang tải thông tin đặt bàn...</div> : null}

        {reservation.data ? (
          <>
            <section className="reservation-card preorder-summary-card">
              <div className="preorder-summary-heading">
                <div>
                  <span className="reservation-kicker">Reservation</span>
                  <h2>{reservation.data.reservationCode}</h2>
                </div>
                <span className={`preorder-status preorder-status-${reservation.data.status.toLowerCase()}`}>
                  {statusLabels[reservation.data.status] ?? reservation.data.status}
                </span>
              </div>
              <div className="preorder-summary">
                <Summary label="Khách hàng" value={reservation.data.customerName} />
                <Summary label="Số khách" value={`${reservation.data.partySize} người`} />
                <Summary label="Thời gian" value={formatDateTime(reservation.data.reservedAt)} />
                <Summary label="Mã đặt bàn" value={reservation.data.reservationCode} />
                <Summary label="Chi nhánh" value={menu.data?.branchName ?? `#${reservation.data.branchId}`} />
                <Summary label="Trạng thái" value={statusLabels[reservation.data.status] ?? reservation.data.status} />
              </div>
              {isClosed ? <div className="reservation-next-note">Đặt bàn đã đóng hoặc đã check-in nên không thể sửa món đặt trước.</div> : null}
            </section>

            <section className="preorder-layout" id="preorder-menu">
              <div className="preorder-menu-column">
                <section className="reservation-card preorder-search-card">
                  <div className="preorder-filter-heading">
                    <div>
                      <span className="reservation-kicker">Menu</span>
                      <h2>Thực đơn đặt trước</h2>
                    </div>
                    <span>{filteredDishes.length} món phù hợp</span>
                  </div>
                  <div className="preorder-search-row">
                    <label>
                      <span>Tìm món</span>
                      <input
                        className="form-control menu-search-input"
                        value={search}
                        onChange={(event) => setSearch(event.target.value)}
                        placeholder="Tìm món theo tên, mô tả, danh mục..."
                      />
                    </label>
                    <button type="button" className="btn btn-outline-secondary" onClick={resetFilters} disabled={!activeFilters}>
                      Xóa lọc
                    </button>
                  </div>
                  <div className="preorder-category-pills" aria-label="Danh mục món">
                    <button type="button" className={selectedCategoryId === "all" ? "active" : ""} onClick={() => setSelectedCategoryId("all")}>
                      Tất cả
                    </button>
                    {categories.map((category) => (
                      <button
                        key={category.categoryId}
                        type="button"
                        className={selectedCategoryId === category.categoryId ? "active" : ""}
                        onClick={() => setSelectedCategoryId(category.categoryId)}
                      >
                        {category.categoryName}
                      </button>
                    ))}
                  </div>
                </section>

                {!hasReservationBranch ? <div className="reservation-card preorder-empty">Đặt bàn chưa gắn chi nhánh nên chưa thể chọn món.</div> : null}
                {menu.isLoading ? <div className="reservation-card preorder-empty">Đang tải menu...</div> : null}
                {menu.error ? <div className="reservation-card preorder-empty">Không thể tải menu đặt trước.</div> : null}

                {recommendedDishes.length > 0 ? (
                  <section className="reservation-card preorder-recommendations">
                    <div className="preorder-section-title">
                      <div>
                        <span className="reservation-kicker">Gợi ý</span>
                        <h2>Món gợi ý cho nhóm</h2>
                      </div>
                      <small>Dựa trên món đặc biệt, món chay và số khách</small>
                    </div>
                    <div className="preorder-recommendation-grid">
                      {recommendedDishes.map((dish) => (
                        <button key={`recommend-${dish.dishId}`} type="button" onClick={() => openDish(dish)} className="preorder-recommendation-card">
                          <img src={resolveDishImage(dish.image, dish.name)} alt={dish.name} onError={handleDishImageError} />
                          <div>
                            <small>{dish.isDailySpecial ? "Món nổi bật" : dish.isVegetarian ? "Phù hợp nhóm" : "Dễ chia sẻ"}</small>
                            <strong>{dish.name}</strong>
                            <span>{vnd(dish.price)}</span>
                          </div>
                          <em>Thêm nhanh</em>
                        </button>
                      ))}
                    </div>
                  </section>
                ) : null}

                <section className="reservation-preorder-dish-grid">
                  {filteredDishes.map((dish) => (
                    <div key={dish.dishId}>
                      <article className={`card dish-card preorder-dish-card ${dish.available ? "" : "dish-card-unavailable"}`}>
                        <button type="button" className="preorder-dish-media" onClick={() => openDish(dish)}>
                          <img className="dish-image" src={resolveDishImage(dish.image, dish.name)} alt={dish.name} onError={handleDishImageError} />
                          {dish.isVegetarian ? <span className="badge-vegetarian"><i className="bi bi-leaf" /> Chay</span> : null}
                          {dish.isDailySpecial ? <span className="badge-top-seller">HOT</span> : null}
                          {!dish.available ? <span className="badge-unavailable">Tạm hết</span> : null}
                        </button>
                        <div className="card-body">
                          <h3 className="card-title">{dish.name}</h3>
                          <div className="dish-suggestion-chip"><i className="bi bi-grid-3x3-gap me-1" />{dish.categoryName}</div>
                          <p className="card-text text-muted small">{normalizeText(dish.description) || "Món ăn đang cập nhật mô tả."}</p>
                          <div className="preorder-dish-actions">
                            <div>
                              <div className="price">{vnd(dish.price)}</div>
                              <small className="text-muted">{normalizeText(dish.unit) || "Phần"}</small>
                            </div>
                            <button type="button" className="btn-add" onClick={() => addDish(dish)} disabled={isClosed || !dish.available}>
                              <i className="bi bi-plus-lg me-1" />
                              Thêm
                            </button>
                          </div>
                          <button type="button" className="btn btn-link dish-detail-link p-0" onClick={() => openDish(dish)}>
                            Xem chi tiết
                          </button>
                        </div>
                      </article>
                    </div>
                  ))}
                  {!menu.isLoading && !menu.error && hasReservationBranch && filteredDishes.length === 0 ? (
                    <div>
                      <div className="reservation-card preorder-empty">
                        {activeFilters ? "Không có món phù hợp bộ lọc." : "Chi nhánh hiện chưa có món khả dụng."}
                      </div>
                    </div>
                  ) : null}
                </section>
              </div>

              <aside className="reservation-card preorder-cart">
                <div className="preorder-cart-title">
                  <div>
                    <span className="reservation-kicker">Draft cart</span>
                    <h2>Món đặt trước</h2>
                  </div>
                  <span>{totalQuantity} món</span>
                </div>

                {cartItems.length === 0 ? (
                  <p className="preorder-empty-text">Chưa có món nào trong danh sách.</p>
                ) : (
                  <div className="preorder-cart-list">
                    {cartItems.map((item) => (
                      <div key={item.dishId} className="preorder-cart-item">
                        <div className="preorder-cart-row">
                          <strong>{item.dishNameSnapshot}</strong>
                          <button type="button" onClick={() => removeItem(item.dishId)} disabled={isClosed}>
                            Xóa
                          </button>
                        </div>
                        <div className="preorder-quantity-row">
                          <button type="button" onClick={() => updateQuantity(item.dishId, item.quantity - 1)} disabled={isClosed || item.quantity <= 1}>-</button>
                          <input
                            type="number"
                            min={1}
                            value={item.quantity}
                            onChange={(event) => updateQuantity(item.dishId, Number(event.target.value))}
                            disabled={isClosed}
                          />
                          <button type="button" onClick={() => updateQuantity(item.dishId, item.quantity + 1)} disabled={isClosed}>+</button>
                          <span>{vnd(item.unitPriceSnapshot * item.quantity)}</span>
                        </div>
                        <label>
                          <span>Ghi chú món</span>
                          <textarea
                            value={item.note}
                            onChange={(event) => updateNote(item.dishId, event.target.value)}
                            disabled={isClosed}
                            placeholder="Ít cay, không hành..."
                          />
                        </label>
                      </div>
                    ))}
                  </div>
                )}

                <div className="preorder-total">
                  <span>Tạm tính</span>
                  <strong>{vnd(subtotal)}</strong>
                </div>
                <button
                  type="button"
                  className="btn btn-danger preorder-save-btn"
                  onClick={handleSave}
                  disabled={isClosed || cartItems.length === 0 || savePreOrder.isPending}
                >
                  {savePreOrder.isPending ? "Đang lưu..." : "Lưu món đặt trước"}
                </button>
                <p className="reservation-next-note">Danh sách này chưa gửi bếp, chưa tạo đơn hàng thật và chưa giữ bàn.</p>
              </aside>
            </section>
          </>
        ) : null}
      </main>

      {selectedDish ? (
        <div className="modal fade show d-block menu-static-modal dish-detail-overlay" tabIndex={-1} aria-modal="true" role="dialog" onClick={() => setSelectedDish(null)}>
          <div className="modal-dialog modal-dialog-centered menu-dish-modal preorder-detail-modal" onClick={(event) => event.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header dish-detail-header">
                <h5 className="modal-title">
                  {selectedDish.name}
                  {selectedDish.isVegetarian ? <span className="badge bg-success ms-2">Chay</span> : null}
                </h5>
                <button type="button" className="btn-close" aria-label="Đóng" onClick={() => setSelectedDish(null)} />
              </div>
              <div className="modal-body dish-detail-body preorder-detail-body">
                <div className="dish-detail-media preorder-detail-media">
                  <img className="img-fluid rounded menu-dish-modal-image" src={resolveDishImage(selectedDish.image, selectedDish.name)} alt={selectedDish.name} onError={handleDishImageError} />
                </div>
                <div className="preorder-detail-panel">
                  <div className="preorder-detail-meta">
                    <span>{selectedDish.categoryName}</span>
                    <strong>{vnd(selectedDish.price)}</strong>
                  </div>
                  <p className="text-muted dish-detail-description">{normalizeText(selectedDish.description) || "Món ăn đang cập nhật mô tả."}</p>
                  <div className="preorder-ingredients-box">
                    <h6>Thành phần</h6>
                    {selectedDish.ingredients && selectedDish.ingredients.length > 0 ? (
                      <ul className="small mb-0 dish-detail-ingredients">
                        {selectedDish.ingredients.map((ingredient) => (
                          <li key={`${selectedDish.dishId}-${ingredient.name}`}>
                            {normalizeText(ingredient.name)}: <strong>{ingredient.quantity.toLocaleString("vi-VN")} {normalizeText(ingredient.unit)}</strong>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <div className="text-muted small">Thông tin thành phần chưa khả dụng.</div>
                    )}
                  </div>
                  <div className="preorder-modal-controls">
                    <label>
                      <span>Số lượng</span>
                      <div className="preorder-quantity-row">
                        <button type="button" onClick={() => setModalQuantity((value) => Math.max(1, value - 1))}>-</button>
                        <input type="number" min={1} value={modalQuantity} onChange={(event) => setModalQuantity(Math.max(1, Number(event.target.value) || 1))} />
                        <button type="button" onClick={() => setModalQuantity((value) => value + 1)}>+</button>
                      </div>
                    </label>
                    <label>
                      <span>Ghi chú</span>
                      <textarea value={modalNote} onChange={(event) => setModalNote(event.target.value)} placeholder="Ít cay, không hành..." />
                    </label>
                  </div>
                </div>
              </div>
              <div className="modal-footer preorder-detail-footer">
                <div>
                  <span>Tạm tính</span>
                  <strong>{vnd(selectedDish.price * modalQuantity)}</strong>
                </div>
                <div className="preorder-detail-footer-actions">
                  <button type="button" className="btn btn-secondary" onClick={() => setSelectedDish(null)}>Đóng</button>
                  <button type="button" className="btn btn-primary" disabled={isClosed || !selectedDish.available} onClick={addSelectedDish}>
                    <i className="bi bi-plus-lg me-1" />
                    Thêm vào đặt trước
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <div className="preorder-summary-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
