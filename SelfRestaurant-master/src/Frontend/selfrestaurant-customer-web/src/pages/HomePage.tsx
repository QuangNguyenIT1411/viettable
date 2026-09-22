import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { api } from "../lib/api";
import { clearGuestMenuCart } from "../lib/guestCart";
import { clearPersistentTableContext, savePersistentTableContext } from "../lib/persistentTable";
import { PublicNavbar } from "../components/PublicNavbar";
import { SiteFooter } from "../components/SiteFooter";
import { useAppDialog } from "../components/AppDialog";

const copy = {
  heroTitle: "Trải Nghiệm Ẩm Thực Tinh Tế",
  heroSubtitle: "Khám phá thực đơn phong phú tại các chi nhánh của Self Restaurant.",
  orderNow: "Đặt Món Ngay",
  pendingTableTitle: "Bạn còn bàn đang chờ",
  pendingTableSuffix: "Quay lại để tiếp tục đặt thêm món",
  returnToTable: "Quay Lại Bàn",
  resetting: "Đang reset...",
  reset: "Reset",
  selectBranch: "Chọn Chi Nhánh",
  selectTable: "Chọn Bàn",
  placeOrder: "Đặt Món",
  step1: "Bước 1: Chọn Chi Nhánh",
  step2: "Bước 2: Chọn Bàn",
  searchPlaceholder: "Tìm kiếm chi nhánh theo tên hoặc địa chỉ...",
  missingAddress: "Cha cp nht a ch",
  noBranchMatch: "Không tìm thấy chi nhánh phù hợp",
  tryAnotherKeyword: "Vui lòng thử từ khóa khác",
  branchLoadFailed: "Không thể tải danh sách chi nhánh lúc này.",
  noBranches: "Hiện chưa có chi nhánh hoạt động để phục vụ.",
  noBranchesHint: "Vui lòng quay lại sau hoặc liên hệ nhà hàng để được hỗ trợ.",
  backToBranches: "Quay lại chọn chi nhánh",
  selectedBranch: "Chi nhánh đã chọn",
  loading: "Đang tải...",
  loadingData: "Đang tải dữ liệu...",
  table: "Bàn",
  seats: "chỗ",
  tableLoadFailed: "Không thể tải danh sách bàn của chi nhánh này.",
  noTables: "Chi nhánh này hiện chưa có bàn để chọn.",
  contextFailed: "Không thể chọn bàn vào lúc này. Vui lòng thử lại.",
  allTablesBusy: "Hiện tại tất cả bàn ở chi nhánh này đều đang bận. Bạn vẫn có thể xem danh sách để chờ bàn trống.",
  resetBody: "Bạn có chắc muốn reset bàn này?",
} as const;

export function HomePage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const [selectedBranchId, setSelectedBranchId] = useState<number | null>(null);
  const [branchSearch, setBranchSearch] = useState("");
  const { confirm, Dialog } = useAppDialog();
  const forceNewOrderFlow = searchParams.get("flow") === "new-order";

  const { data: session } = useQuery({ queryKey: ["session"], queryFn: api.getSession });
  const branches = useQuery({ queryKey: ["branches"], queryFn: api.getBranches });
  const syncSessionFromActiveOrder = useMutation({
    mutationFn: api.syncSessionFromActiveOrder,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["session"] });
      await queryClient.refetchQueries({ queryKey: ["session"], exact: true });
    },
  });
  const tables = useQuery({
    queryKey: ["branchTables", selectedBranchId],
    queryFn: () => api.getBranchTables(selectedBranchId!),
    enabled: !!selectedBranchId,
  });

  const setContext = useMutation({
    mutationFn: api.setContextTable,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["session"] });
      await queryClient.refetchQueries({ queryKey: ["session"], exact: true });
      await queryClient.invalidateQueries({ queryKey: ["order"] });
      await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
      navigate("/Menu/Index");
    },
  });

  const resetCurrentTable = useMutation({
    mutationFn: api.resetCurrentTable,
    onSuccess: async () => {
      clearGuestMenuCart(session?.tableContext);
      if (session?.customer?.customerId) {
        clearPersistentTableContext(session.customer.customerId);
      }
      await queryClient.invalidateQueries({ queryKey: ["session"] });
      await queryClient.refetchQueries({ queryKey: ["session"], exact: true });
      await queryClient.invalidateQueries({ queryKey: ["order"] });
      await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
      setSelectedBranchId(null);
      navigate("/Home/Index");
    },
  });

  const filteredBranches = useMemo(() => {
    const items = branches.data ?? [];
    const keyword = branchSearch.trim().toLowerCase();
    if (!keyword) return items;
    return items.filter((branch) =>
      branch.name.toLowerCase().includes(keyword) ||
      (branch.location ?? "").toLowerCase().includes(keyword),
    );
  }, [branchSearch, branches.data]);

  const selectedBranch = useMemo(
    () => branches.data?.find((branch) => branch.branchId === selectedBranchId) ?? null,
    [branches.data, selectedBranchId],
  );

  const tableList = tables.data?.tables ?? [];
  const allTablesBusy = tableList.length > 0 && tableList.every((table) => !table.isAvailable);
  const showPendingTable = Boolean(session?.tableContext) && !forceNewOrderFlow;
  const currentStep = showPendingTable ? 3 : selectedBranchId ? 2 : 1;

  useEffect(() => {
    if (
      forceNewOrderFlow
      || !session?.authenticated
      || Boolean(session?.tableContext)
      || syncSessionFromActiveOrder.isPending
    ) return;
    syncSessionFromActiveOrder.mutate();
  // Intentionally run when authenticated state flips or page remounts on Home/Index.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [forceNewOrderFlow, session?.authenticated, session?.tableContext]);

  useEffect(() => {
    if (!session?.authenticated || !session.customer) return;

    // Home must stay reachable like MVC. The previous version tried to auto-restore
    // saved table context here and reused the same success path that intentionally
    // enters Menu, which could send users straight back to /Menu/Index after they
    // had explicitly navigated to /Home/Index.
    if (session.tableContext) {
      savePersistentTableContext(session.customer.customerId, session.tableContext);
    }
  }, [session]);

  return (
    <div className="home-page customer-entry-shell">
      <PublicNavbar />

      <header className="hero-section">
        <div className="container">
          <h1>{copy.heroTitle}</h1>
          <p>{copy.heroSubtitle}</p>
          <a href="#branch-selector" className="btn btn-danger btn-lg home-cta-btn">
            {copy.orderNow}
          </a>
        </div>
      </header>

      {showPendingTable ? (
        <div className="container mt-4">
          <div className="continue-table-alert">
            <div className="continue-table-text">
              <h4>
                <i className="fas fa-chair me-2" />
                {copy.pendingTableTitle}
              </h4>
              <p>
                {session?.tableContext?.branchName} - {copy.table}{" "}
                {session?.tableContext?.tableNumber ?? session?.tableContext?.tableId} | {copy.pendingTableSuffix}
              </p>
            </div>
            <div className="continue-table-buttons">
              <Link to="/Menu/Index" className="btn btn-light">
                <i className="fas fa-arrow-right me-1" />
                {copy.returnToTable}
              </Link>
              <button
                type="button"
                className="btn btn-outline-light"
                onClick={async () => {
                  const approved = await confirm({
                    title: "Xác nhận",
                    message: copy.resetBody,
                    confirmLabel: "Đồng ý",
                    cancelLabel: "Không",
                  });
                  if (approved) {
                    resetCurrentTable.mutate();
                  }
                }}
                disabled={resetCurrentTable.isPending}
              >
                <i className="fas fa-times me-1" />
                {resetCurrentTable.isPending ? copy.resetting : copy.reset}
              </button>
            </div>
          </div>
        </div>
      ) : null}

      <div className="container mt-4">
        <div className="step-indicator customer-step-indicator" id="step-indicator">
          <div className={`step ${currentStep === 1 ? "active" : currentStep > 1 ? "completed" : ""}`} id="step-1">
            <div className="step-number">1</div>
            <div className="step-text">{copy.selectBranch}</div>
          </div>
          <div className="step-arrow">&rarr;</div>
          <div className={`step ${currentStep === 2 ? "active" : currentStep > 2 ? "completed" : ""}`} id="step-2">
            <div className="step-number">2</div>
            <div className="step-text">{copy.selectTable}</div>
          </div>
          <div className="step-arrow">&rarr;</div>
          <div className={`step ${currentStep === 3 ? "active" : ""}`} id="step-3">
            <div className="step-number">3</div>
            <div className="step-text">{copy.placeOrder}</div>
          </div>
        </div>
      </div>

      <section className={`section-container customer-selection-panel ${selectedBranchId ? "d-none" : ""}`} id="branch-selector">
        <div className="container">
          <h2 className="section-title">{copy.step1}</h2>

          <div className="search-box">
            <div className="input-group">
              <span className="input-group-text bg-white border-end-0">
                <i className="fas fa-search text-muted" />
              </span>
              <input
                type="text"
                id="branch-search"
                className="form-control search-input border-start-0"
                placeholder={copy.searchPlaceholder}
                value={branchSearch}
                onChange={(e) => setBranchSearch(e.target.value)}
              />
            </div>
          </div>

          <div id="branch-buttons">
            {filteredBranches.map((branch) => (
              <button
                key={branch.branchId}
                type="button"
                className={`btn branch-btn ${selectedBranchId === branch.branchId ? "btn-danger" : "btn-outline-danger"}`}
                data-branch-id={branch.branchId}
                data-branch-name={branch.name}
                data-branch-location={branch.location ?? ""}
                onClick={() => setSelectedBranchId(branch.branchId)}
              >
                <i className="fas fa-map-marker-alt me-2" />
                {branch.name}
                <br />
                <small className="text-muted">{branch.location || copy.missingAddress}</small>
              </button>
            ))}
          </div>

          {branches.isPending ? (
            <div className="no-results">
              <i className="fas fa-spinner fa-spin" />
              <p>{copy.loadingData}</p>
            </div>
          ) : null}

          {branches.error ? (
            <div className="no-results">
              <i className="fas fa-circle-exclamation" />
              <p>{copy.branchLoadFailed}</p>
              <small className="text-muted">{(branches.error as Error).message}</small>
            </div>
          ) : null}

          {!branches.isPending && !branches.error && filteredBranches.length === 0 && branches.data && branches.data.length === 0 ? (
            <div className="no-results">
              <i className="fas fa-store-slash" />
              <p>{copy.noBranches}</p>
              <small className="text-muted">{copy.noBranchesHint}</small>
            </div>
          ) : null}

          {filteredBranches.length === 0 && branchSearch.trim() ? (
            <div className="no-results" id="no-results">
              <i className="fas fa-search" />
              <p>{copy.noBranchMatch}</p>
              <small className="text-muted">{copy.tryAnotherKeyword}</small>
            </div>
          ) : null}
        </div>
      </section>

      <section className={`section-container customer-selection-panel ${selectedBranchId ? "" : "d-none"}`} id="table-selector">
        <div className="container">
          <button type="button" className="btn btn-outline-secondary back-btn" onClick={() => setSelectedBranchId(null)}>
            <i className="fas fa-arrow-left me-2" />
            {copy.backToBranches}
          </button>
          <h2 className="section-title">{copy.step2}</h2>
          <div className="mb-3">
            <span className="info-badge">
              <i className="fas fa-store me-2" />
              <strong id="selected-branch-name">
                {selectedBranch?.name ?? tables.data?.branchName ?? copy.selectedBranch}
              </strong>
            </span>
          </div>

          {setContext.error ? (
            <div className="alert alert-danger home-inline-alert" role="alert">
              <i className="fas fa-circle-exclamation me-2" />
              {(setContext.error as Error).message || copy.contextFailed}
            </div>
          ) : null}

          {tables.error ? (
            <div className="alert alert-danger home-inline-alert" role="alert">
              <i className="fas fa-circle-exclamation me-2" />
              {(tables.error as Error).message || copy.tableLoadFailed}
            </div>
          ) : null}

          {!tables.isLoading ? (
            <div id="table-grid" className="row row-cols-2 row-cols-md-3 row-cols-lg-4 g-3">
              {tableList.map((table) => (
                <div key={table.tableId} className="col">
                  <button
                    type="button"
                    className={`table-card ${table.isAvailable ? "available" : "occupied"} ${
                      session?.tableContext?.tableId === table.tableId ? "selected" : ""
                    }`}
                    onClick={() => setContext.mutate({ tableId: table.tableId, branchId: table.branchId })}
                    disabled={!table.isAvailable || setContext.isPending}
                    aria-label={`${copy.table} ${table.displayTableNumber}`}
                  >
                    <div className="table-icon">
                      <i className="fas fa-chair" />
                    </div>
                    <h5>
                      {copy.table} {table.displayTableNumber}
                    </h5>
                    <p className="mb-1">
                      <i className="fas fa-users me-2" />
                      {table.numberOfSeats} {copy.seats}
                    </p>
                    <span className={`badge bg-${table.isAvailable ? "success" : "danger"}`}>{table.statusName}</span>
                  </button>
                </div>
              ))}
            </div>
          ) : null}

          {!tables.isLoading && !tables.error && tableList.length === 0 ? (
            <div className="no-results">
              <i className="fas fa-chair" />
              <p>{copy.noTables}</p>
            </div>
          ) : null}

          {!tables.isLoading && !tables.error && allTablesBusy ? (
            <div className="alert alert-warning home-inline-alert" role="alert">
              <i className="fas fa-hourglass-half me-2" />
              {copy.allTablesBusy}
            </div>
          ) : null}
        </div>
      </section>

      <div id="loading-spinner" className={`loading-spinner ${selectedBranchId && tables.isLoading ? "" : "d-none"}`}>
        <div className="spinner-border text-danger" role="status">
          <span className="visually-hidden">{copy.loading}</span>
        </div>
        <p className="mt-3 text-muted">{copy.loadingData}</p>
      </div>
      <SiteFooter />
      <Dialog />
    </div>
  );
}
