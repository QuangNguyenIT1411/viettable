import { useEffect, useMemo, useState, type ChangeEvent } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { AdminLayout } from "../components/AdminLayout";
import { AdminPagination } from "../components/AdminPagination";
import { useAppDialog } from "../components/AppDialog";
import { adminApi } from "../lib/api";
import { useAutoDismissMessage } from "../lib/useAutoDismissMessage";
import type {
  AdminCategoriesScreenDto,
  AdminDashboardDto,
  AdminDishDto,
  AdminDishIngredientLineDto,
  AdminDishesScreenDto,
  AdminReportsScreenDto,
  AdminTableDto,
  AdminTablesScreenDto,
  AdminUnitDto,
  StaffSessionUserDto,
} from "../lib/types";

type Props = { onLogout: () => Promise<void> };
type SectionKey = "overview" | "categories" | "dishes" | "tables" | "reports" | "settings";

const emptyCategoryForm = { name: "", description: "", displayOrder: "0" };
const emptyTableForm = { branchId: "", numberOfSeats: "4", statusId: "" };
const emptyDishForm = {
  name: "",
  price: "10000",
  categoryId: "",
  description: "",
  unit: "dia",
  isVegetarian: false,
  isDailySpecial: false,
  available: true,
};
const DISH_PAGE_SIZE = 10;
const TABLE_PAGE_SIZE = 10;
const DISH_INGREDIENT_PAGE_SIZE_OPTIONS = [10, 20, 50] as const;
const DELETE_REQUIRES_INACTIVE_MESSAGE = "Vui l\u00f2ng v\u00f4 hi\u1ec7u h\u00f3a tr\u01b0\u1edbc khi x\u00f3a.";
const DELETE_DISH_REQUIRES_SUSPENDED_MESSAGE = "Vui l\u00f2ng t\u1ea1m ng\u01b0ng m\u00f3n \u0103n tr\u01b0\u1edbc khi x\u00f3a.";
const DELETE_TABLE_REQUIRES_HIDDEN_MESSAGE = "Vui lòng ẩn bàn trước khi xóa";
const HARD_DELETE_CONFIRM_MESSAGE = "B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n x\u00f3a d\u1eef li\u1ec7u n\u00e0y kh\u1ecfi h\u1ec7 th\u1ed1ng kh\u00f4ng?";

function formatDateTime(value?: string | null) {
  if (!value) return "Chưa cập nhật";
  return new Date(value).toLocaleString("vi-VN");
}

function buildQrTargetUrl(qrCode?: string | null) {
  if (!qrCode) return "";
  const encoded = encodeURIComponent(qrCode);
  if (typeof window === "undefined") {
    return `/Menu/FromQr?code=${encoded}`;
  }
  return `${window.location.origin}/Menu/FromQr?code=${encoded}`;
}

function buildQrImageUrl(qrCode?: string | null) {
  const targetUrl = buildQrTargetUrl(qrCode);
  return targetUrl
    ? `https://api.qrserver.com/v1/create-qr-code/?size=140x140&data=${encodeURIComponent(targetUrl)}`
    : "";
}

function resolveSection(pathname: string): SectionKey {
  const normalized = pathname.toLowerCase();
  if (normalized.includes("/admin/categories")) return "categories";
  if (normalized.includes("/admin/dishes")) return "dishes";
  if (normalized.includes("/admin/tablesqr")) return "tables";
  if (normalized.includes("/admin/reports")) return "reports";
  if (normalized.includes("/admin/settings")) return "settings";
  return "overview";
}

function resolveHeading(pathname: string): { title: string; description: string } {
  const normalized = pathname.toLowerCase();
  if (normalized.includes("/admin/categories")) return { title: "Qu\u1ea3n l\u00fd danh m\u1ee5c", description: "Qu\u1ea3n l\u00fd danh m\u1ee5c v\u00e0 \u0111\u01a1n v\u1ecb m\u00f3n \u0103n." };
  if (normalized.includes("/admin/dishes")) return { title: "Qu\u1ea3n l\u00fd m\u00f3n \u0103n", description: "Qu\u1ea3n l\u00fd m\u00f3n \u0103n, h\u00ecnh \u1ea3nh v\u00e0 th\u00e0nh ph\u1ea7n m\u00f3n \u0103n." };
  if (normalized.includes("/admin/tablesqr")) return { title: "Qu\u1ea3n l\u00fd b\u00e0n & m\u00e3 QR", description: "Qu\u1ea3n l\u00fd b\u00e0n \u0103n v\u00e0 m\u00e3 QR." };
  if (normalized.includes("/admin/reports/topdishes")) return { title: "M\u00f3n \u0103n g\u1ecdi nhi\u1ec1u", description: "Top m\u00f3n theo s\u1ed1 l\u01b0\u1ee3ng b\u00e1n ra." };
  if (normalized.includes("/admin/reports")) return { title: "B\u00e1o c\u00e1o doanh thu", description: "T\u1ed5ng quan doanh thu theo ng\u00e0y v\u00e0 chi nh\u00e1nh." };
  if (normalized.includes("/admin/settings")) return { title: "C\u00e0i \u0111\u1eb7t t\u00e0i kho\u1ea3n", description: "C\u1eadp nh\u1eadt th\u00f4ng tin c\u00e1 nh\u00e2n v\u00e0 m\u1eadt kh\u1ea9u." };
  return { title: "T\u1ed5ng quan qu\u1ea3n tr\u1ecb", description: "T\u1ed5ng quan \u0111\u01a1n h\u00e0ng, nh\u00e2n s\u1ef1, b\u00e0n \u0103n v\u00e0 chi nh\u00e1nh." };
}
export function AdminConsolePage({ onLogout }: Props) {
  const location = useLocation();
  const navigate = useNavigate();

  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [dashboard, setDashboard] = useState<AdminDashboardDto | null>(null);
  const [categories, setCategories] = useState<AdminCategoriesScreenDto | null>(null);
  const [dishes, setDishes] = useState<AdminDishesScreenDto | null>(null);
  const [dishUnits, setDishUnits] = useState<AdminUnitDto[]>([]);
  const [tablesData, setTablesData] = useState<AdminTablesScreenDto | null>(null);
  const [reports, setReports] = useState<AdminReportsScreenDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);

  const [categoryForm, setCategoryForm] = useState(emptyCategoryForm);
  const [categoryEditForm, setCategoryEditForm] = useState({ categoryId: 0, name: "", description: "", displayOrder: "0", isActive: true });
  const [categoryHelperOpen, setCategoryHelperOpen] = useState(false);
  const [tableForm, setTableForm] = useState(emptyTableForm);
  const [tableEditForm, setTableEditForm] = useState({ tableId: 0, branchId: "", numberOfSeats: "4", statusId: "", qrCode: "", isActive: true });
  const [dishForm, setDishForm] = useState(emptyDishForm);
  const [dishEditForm, setDishEditForm] = useState({ dishId: 0, name: "", price: "10000", categoryId: "", description: "", unit: "dia", image: "", isVegetarian: false, isDailySpecial: false, available: true, isActive: true });
  const [dishCreateImageFile, setDishCreateImageFile] = useState<File | null>(null);
  const [dishCreateImagePreview, setDishCreateImagePreview] = useState("");
  const [dishEditImageFile, setDishEditImageFile] = useState<File | null>(null);
  const [dishEditImagePreview, setDishEditImagePreview] = useState("");
  const [dishSaving, setDishSaving] = useState(false);
  const [dishIngredientEditor, setDishIngredientEditor] = useState<{ dishId: number; dishName: string; items: AdminDishIngredientLineDto[] } | null>(null);
  const [settingsDraft, setSettingsDraft] = useState({ name: "", phone: "", email: "" });
  const [passwordEditor, setPasswordEditor] = useState({ currentPassword: "", newPassword: "", confirmPassword: "" });

  const [reportBranchFilter, setReportBranchFilter] = useState("ALL");
  const [tableBranchFilter, setTableBranchFilter] = useState("ALL");
  const [tableSearch, setTableSearch] = useState("");
  const [tablePage, setTablePage] = useState(1);
  const [dishSearch, setDishSearch] = useState("");
  const [dishCategoryFilter, setDishCategoryFilter] = useState("ALL");
  const [dishOnlyVegetarian, setDishOnlyVegetarian] = useState(false);
  const [dishPage, setDishPage] = useState(1);
  const [dishIngredientPage, setDishIngredientPage] = useState(1);
  const [dishIngredientPageSize, setDishIngredientPageSize] = useState<(typeof DISH_INGREDIENT_PAGE_SIZE_OPTIONS)[number]>(10);
  const [tableSummaryItems, setTableSummaryItems] = useState<AdminTableDto[]>([]);
  const [initialized, setInitialized] = useState(false);
  const { confirm, Dialog } = useAppDialog();

  const section = resolveSection(location.pathname);
  const pageHeading = resolveHeading(location.pathname);
  const normalizedPath = location.pathname.toLowerCase();
  const categoryEditMatch = normalizedPath.match(/\/admin\/categories\/edit\/(\d+)/);
  const categoryEditId = categoryEditMatch ? Number(categoryEditMatch[1]) : 0;
  const isCategoryCreatePage = normalizedPath.includes("/admin/categories/create");
  const isCategoryEditPage = categoryEditId > 0;
  const isCategoryUnitsPage = normalizedPath.includes("/admin/categories/units");
  const isCategoryStatusesPage = normalizedPath.includes("/admin/categories/statuses");
  const isCategoryIndexPage = section === "categories" && !isCategoryCreatePage && !isCategoryEditPage && !isCategoryUnitsPage && !isCategoryStatusesPage;
  const isDishCreatePage = location.pathname.toLowerCase().includes("/admin/dishes/create");
  const isDishEditPage = location.pathname.toLowerCase().includes("/admin/dishes/edit");
  const isDishIngredientsPage = location.pathname.toLowerCase().includes("/admin/dishes/ingredients");
  const isTableEditPage = location.pathname.toLowerCase().includes("/admin/tablesqr/edit");
  const isTableQrPage = location.pathname.toLowerCase().includes("/admin/tablesqr/qr");
  const queryParams = new URLSearchParams(location.search);
  const tableQrFilterId = queryParams.get("tableId");
  const dishIngredientQueryId = Number(queryParams.get("dishId") || "0");
  const isRevenuePage = location.pathname.toLowerCase().includes("/admin/reports/revenue");
  const isTopDishesPage = location.pathname.toLowerCase().includes("/admin/reports/topdishes");

  const reportBranchOptions = useMemo(() => {
    const rows = reports?.revenue.revenueByBranchDate ?? [];
    const map = new Map<string, { label: string; count: number }>();
    rows.forEach((row) => {
      const current = map.get(String(row.branchId));
      map.set(String(row.branchId), { label: row.branchName, count: (current?.count ?? 0) + 1 });
    });
    return [{ key: "ALL", label: "Tất cả", count: rows.length }, ...Array.from(map.entries()).map(([key, value]) => ({ key, label: value.label, count: value.count }))];
  }, [reports]);

  const filteredRevenueRows = useMemo(() => {
    const rows = reports?.revenue.revenueByBranchDate ?? [];
    return reportBranchFilter === "ALL" ? rows : rows.filter((row) => String(row.branchId) === reportBranchFilter);
  }, [reportBranchFilter, reports]);

  const filteredRevenueTotal = useMemo(() => filteredRevenueRows.reduce((sum, row) => sum + row.totalRevenue, 0), [filteredRevenueRows]);

  const dishIngredientTotalItems = dishIngredientEditor?.items.length ?? 0;
  const dishIngredientTotalPages = Math.max(1, Math.ceil(dishIngredientTotalItems / dishIngredientPageSize));
  const dishIngredientPageStart = (dishIngredientPage - 1) * dishIngredientPageSize;
  const pagedDishIngredientItems = useMemo(
    () => dishIngredientEditor?.items.slice(dishIngredientPageStart, dishIngredientPageStart + dishIngredientPageSize) ?? [],
    [dishIngredientEditor, dishIngredientPageStart, dishIngredientPageSize],
  );

  useEffect(() => {
    setDishIngredientPage((currentPage) => Math.min(Math.max(currentPage, 1), dishIngredientTotalPages));
  }, [dishIngredientTotalPages]);

  useEffect(() => {
    if (!isDishIngredientsPage || dishIngredientEditor || dishIngredientQueryId <= 0) return;

    let cancelled = false;
    adminApi.getDishIngredients(dishIngredientQueryId)
      .then((recipe) => {
        if (cancelled) return;
        setDishIngredientPage(1);
        setDishIngredientEditor({ dishId: recipe.dishId, dishName: recipe.dishName, items: recipe.ingredients });
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : "Kh?ng th? t?i nguy?n li?u m?n ?n.");
      });

    return () => {
      cancelled = true;
    };
  }, [dishIngredientEditor, dishIngredientQueryId, isDishIngredientsPage]);

  async function loadTableSummaryData(scopedBranchId?: number) {
    const firstPage = await adminApi.getTables("", scopedBranchId, 1, 100);
    const items = [...firstPage.tables.items];
    if (firstPage.tables.totalPages > 1) {
      const extraPages = await Promise.all(
        Array.from({ length: firstPage.tables.totalPages }, (_, index) => index + 1)
          .slice(1)
          .map((pageNumber) => adminApi.getTables("", scopedBranchId, pageNumber, 100)),
      );
      extraPages.forEach((pageResult) => items.push(...pageResult.tables.items));
    }
    setTableSummaryItems(items);
  }

  async function loadStaticData() {
    const [session, nextDashboard, nextCategories, nextReports] = await Promise.all([
      adminApi.getSession(),
      adminApi.getDashboard(),
      adminApi.getCategories(),
      adminApi.getReports(),
    ]);
    const nextStaff = session.staff ?? null;
    setStaff(nextStaff);
    if (nextStaff) {
      setTableBranchFilter(String(nextStaff.branchId));
      setReportBranchFilter(String(nextStaff.branchId));
    }
    setDashboard(nextDashboard);
    setCategories(nextCategories);
    setReports(nextReports);
    setSettingsDraft({
      name: nextDashboard.settings.name,
      phone: nextDashboard.settings.phone ?? "",
      email: nextDashboard.settings.email ?? "",
    });
    return nextStaff;
  }

  async function loadDishesData() {
    const nextDishes = await adminApi.getDishes(
      dishSearch,
      dishCategoryFilter !== "ALL" ? Number(dishCategoryFilter) : undefined,
      dishPage,
      DISH_PAGE_SIZE,
      false,
      dishOnlyVegetarian,
    );
    setDishes(nextDishes);
  }

  async function loadDishUnitsData() {
    const firstPage = await adminApi.getUnits("", 1, 100, false);
    const items = [...firstPage.items];
    if (firstPage.totalPages > 1) {
      const extraPages = await Promise.all(
        Array.from({ length: firstPage.totalPages }, (_, index) => index + 1)
          .slice(1)
          .map((pageNumber) => adminApi.getUnits("", pageNumber, 100, false)),
      );
      extraPages.forEach((pageResult) => items.push(...pageResult.items));
    }
    setDishUnits(items);
  }

  async function loadTablesPageData() {
    const scopedBranchId = staff?.branchId ?? (tableBranchFilter !== "ALL" ? Number(tableBranchFilter) : undefined);
    const nextTables = await adminApi.getTables(
      tableSearch,
      scopedBranchId,
      tablePage,
      TABLE_PAGE_SIZE,
    );
    setTablesData(nextTables);
  }

  async function loadAll(showSpinner = true) {
    if (showSpinner) {
      setLoading(true);
    }
    setError(null);
    try {
      const nextStaff = await loadStaticData();
      await Promise.all([
        loadDishesData(),
        loadDishUnitsData(),
        loadTablesPageData(),
        loadTableSummaryData(nextStaff?.branchId),
      ]);
      setInitialized(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải dữ liệu quản trị.");
    } finally {
      if (showSpinner) {
        setLoading(false);
      }
    }
  }

  useEffect(() => {
    void loadAll();
  }, []);

  useEffect(() => {
    if (!initialized) return;
    void loadDishesData().catch((err) => setError(err instanceof Error ? err.message : "Không thể tải danh sách món ăn."));
  }, [initialized, dishSearch, dishCategoryFilter, dishOnlyVegetarian, dishPage]);

  useEffect(() => {
    if (!initialized) return;
    void loadTablesPageData().catch((err) => setError(err instanceof Error ? err.message : "Không thể tải danh sách bàn."));
  }, [initialized, staff?.branchId, tableSearch, tableBranchFilter, tablePage]);

  useEffect(() => {
    const flash = (location.state as { message?: string } | null)?.message;
    if (flash) {
      setMessage(flash);
      navigate(location.pathname + location.search, { replace: true, state: null });
    }
  }, [location.pathname, location.search, location.state, navigate]);

  useEffect(() => {
    if (tablesData && !tableForm.branchId) {
      setTableForm({
        branchId: String(tablesData.branches[0]?.branchId ?? ""),
        numberOfSeats: "4",
        statusId: String(tablesData.tableStatuses[0]?.statusId ?? ""),
      });
    }
  }, [tableForm.branchId, tablesData]);

  useEffect(() => {
    if (dishes && !dishForm.categoryId) {
      setDishForm((current) => ({ ...current, categoryId: String(dishes.categories[0]?.categoryId ?? "") }));
    }
  }, [dishForm.categoryId, dishes]);

  useEffect(() => {
    if (dishUnits.length === 0) return;
    setDishForm((current) => {
      if (current.unit && dishUnits.some((unit) => unit.name === current.unit)) {
        return current;
      }
      return { ...current, unit: dishUnits[0].name };
    });
  }, [dishUnits]);

  useEffect(() => {
    if (!dishCreateImageFile) {
      setDishCreateImagePreview("");
      return;
    }

    const previewUrl = URL.createObjectURL(dishCreateImageFile);
    setDishCreateImagePreview(previewUrl);
    return () => URL.revokeObjectURL(previewUrl);
  }, [dishCreateImageFile]);

  useEffect(() => {
    if (!dishEditImageFile) {
      setDishEditImagePreview("");
      return;
    }

    const previewUrl = URL.createObjectURL(dishEditImageFile);
    setDishEditImagePreview(previewUrl);
    return () => URL.revokeObjectURL(previewUrl);
  }, [dishEditImageFile]);

  useEffect(() => {
    if (!isCategoryEditPage || !categories) return;
    const selected = categories.categories.find((category) => category.categoryId === categoryEditId);
    if (!selected) {
      setCategoryEditForm({ categoryId: 0, name: "", description: "", displayOrder: "0", isActive: true });
      setError("Không tìm thấy danh mục cần chỉnh sửa.");
      return;
    }

    setCategoryEditForm({
      categoryId: selected.categoryId,
      name: selected.name,
      description: selected.description ?? "",
      displayOrder: String(selected.displayOrder),
      isActive: selected.isActive,
    });
  }, [categories, categoryEditId, isCategoryEditPage]);

  function showMessage(nextMessage: string) {
    setError(null);
    setMessage(nextMessage);
  }

  async function canHardDelete(isActive: boolean) {
    if (isActive) {
      setMessage(null);
      setError(DELETE_REQUIRES_INACTIVE_MESSAGE);
      return false;
    }

    return await confirm({
      title: "Xác nhận xóa",
      message: HARD_DELETE_CONFIRM_MESSAGE,
      confirmLabel: "Xóa",
      cancelLabel: "Hủy",
      variant: "danger",
    });
  }

  function categoryPayload(category: AdminCategoriesScreenDto["categories"][number], isActive: boolean) {
    return {
      name: category.name,
      description: category.description ?? "",
      displayOrder: category.displayOrder,
      isActive,
    };
  }

  function dishPayload(dish: AdminDishDto, isActive = dish.isActive) {
    return {
      name: dish.name,
      price: dish.price,
      categoryId: dish.categoryId,
      description: dish.description ?? "",
      unit: dish.unit ?? "dia",
      image: dish.image ?? null,
      isVegetarian: dish.isVegetarian,
      isDailySpecial: dish.isDailySpecial,
      available: dish.available,
      isActive,
    };
  }

  function handleDishImageChange(event: ChangeEvent<HTMLInputElement>, mode: "create" | "edit") {
    const file = event.target.files?.[0] ?? null;
    if (!file) return;

    if (!file.type.startsWith("image/")) {
      setMessage(null);
      setError("Vui lòng chọn tệp hình ảnh hợp lệ.");
      event.target.value = "";
      return;
    }

    setError(null);
    if (mode === "create") {
      setDishCreateImageFile(file);
    } else {
      setDishEditImageFile(file);
    }
  }

  function resetDishCreateImage() {
    setDishCreateImageFile(null);
    setDishCreateImagePreview("");
  }

  function resetDishEditImage() {
    setDishEditImageFile(null);
    setDishEditImagePreview("");
  }

  function renderDishUnitOptions(selectedUnit: string) {
    const trimmedUnit = selectedUnit.trim();
    const hasSelectedUnit = dishUnits.some((unit) => unit.name === trimmedUnit);
    if (dishUnits.length === 0 && !trimmedUnit) {
      return <option value="">Chưa có đơn vị khả dụng</option>;
    }

    return (
      <>
        {trimmedUnit && !hasSelectedUnit ? <option value={trimmedUnit}>{trimmedUnit} (ngừng dùng)</option> : null}
        {dishUnits.map((unit) => <option key={unit.unitId} value={unit.name}>{unit.name}</option>)}
      </>
    );
  }

  function tablePayload(table: AdminTablesScreenDto["tables"]["items"][number], isActive = table.isActive) {
    return {
      branchId: table.branchId,
      numberOfSeats: table.numberOfSeats,
      statusId: table.statusId,
      isActive,
    };
  }

  async function refreshAndShow(action: Promise<{ message?: string } | unknown>, successMessage?: string) {
    try {
      const response = await action as { message?: string };
      const nextMessage = successMessage ?? response?.message;
      if (nextMessage) showMessage(nextMessage);
      await loadAll();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật dữ liệu.");
    }
  }

  function openCategoryEditPage(category: AdminCategoriesScreenDto["categories"][number]) {
    setCategoryEditForm({
      categoryId: category.categoryId,
      name: category.name,
      description: category.description ?? "",
      displayOrder: String(category.displayOrder),
      isActive: category.isActive,
    });
    navigate(`/Admin/Categories/Edit/${category.categoryId}`);
  }

  async function handleCreateCategory() {
    if (!categoryForm.name.trim()) {
      setError("Tên danh mục không được để trống.");
      return;
    }

    try {
      const response = await adminApi.createCategory({
        name: categoryForm.name.trim(),
        description: categoryForm.description.trim(),
        displayOrder: Number(categoryForm.displayOrder || "0"),
      });
      setCategoryForm(emptyCategoryForm);
      await loadAll(false);
      navigate("/Admin/Categories/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tạo danh mục.");
    }
  }

  async function handleUpdateCategory() {
    if (!categoryEditForm.categoryId) {
      setError("Không tìm thấy danh mục cần chỉnh sửa.");
      return;
    }

    if (!categoryEditForm.name.trim()) {
      setError("Tên danh mục không được để trống.");
      return;
    }

    try {
      const response = await adminApi.updateCategory(categoryEditForm.categoryId, {
        name: categoryEditForm.name.trim(),
        description: categoryEditForm.description.trim(),
        displayOrder: Number(categoryEditForm.displayOrder || "0"),
        isActive: categoryEditForm.isActive,
      });
      setCategoryEditForm({ categoryId: 0, name: "", description: "", displayOrder: "0", isActive: true });
      await loadAll(false);
      navigate("/Admin/Categories/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật danh mục.");
    }
  }

  async function removeDish(dish: AdminDishDto) {
    // Chỉ cho xóa món đã tạm ngưng (available = false)
    if (dish.available) {
      setMessage(null);
      setError(DELETE_DISH_REQUIRES_SUSPENDED_MESSAGE);
      return;
    }

    if (!(await confirm({ title: "Xác nhận xóa", message: HARD_DELETE_CONFIRM_MESSAGE, variant: "danger" }))) {
      return;
    }

    await refreshAndShow(adminApi.deleteDish(dish.dishId));

    if (dishEditForm.dishId === dish.dishId) {
      setDishEditForm({ dishId: 0, name: "", price: "10000", categoryId: "", description: "", unit: "dia", image: "", isVegetarian: false, isDailySpecial: false, available: true, isActive: true });
      resetDishEditImage();
      navigate("/Admin/Dishes/Index");
    }
  }

  async function removeCategory(category: AdminCategoriesScreenDto["categories"][number]) {
    if (!(await canHardDelete(category.isActive))) {
      return;
    }

    await refreshAndShow(adminApi.deleteCategory(category.categoryId));
  }

  async function removeTable(table: AdminTablesScreenDto["tables"]["items"][number]) {
    if (table.isActive) {
      setMessage(null);
      setError(DELETE_TABLE_REQUIRES_HIDDEN_MESSAGE);
      return;
    }

    if (!(await confirm({ title: "Xác nhận xóa", message: HARD_DELETE_CONFIRM_MESSAGE, confirmLabel: "Xóa", cancelLabel: "Hủy", variant: "danger" }))) {
      return;
    }

    await refreshAndShow(adminApi.deleteTable(table.tableId));
  }

  async function setCategoryActive(category: AdminCategoriesScreenDto["categories"][number], isActive: boolean) {
    await refreshAndShow(
      adminApi.updateCategory(category.categoryId, categoryPayload(category, isActive)),
      isActive ? "Đã bật lại danh mục." : "Đã vô hiệu hóa danh mục.",
    );
  }

  async function setDishActive(dish: AdminDishDto, isActive: boolean) {
    await refreshAndShow(
      adminApi.updateDish(dish.dishId, dishPayload(dish, isActive)),
      isActive ? "Đã bật lại món ăn." : "Đã vô hiệu hóa món ăn.",
    );
  }

  async function setTableActive(table: AdminTablesScreenDto["tables"]["items"][number], isActive: boolean) {
    await refreshAndShow(
      adminApi.updateTable(table.tableId, tablePayload(table, isActive)),
      isActive ? "Đã hiển thị lại bàn." : "Đã ẩn bàn.",
    );
  }

  function openTableEditPage(table: AdminTablesScreenDto["tables"]["items"][number]) {
    setTableEditForm({
      tableId: table.tableId,
      branchId: String(table.branchId),
      numberOfSeats: String(table.numberOfSeats),
      statusId: String(table.statusId),
      qrCode: table.qrCode ?? "",
      isActive: table.isActive,
    });
    navigate("/Admin/TablesQR/Edit");
  }

  function openDishEditPage(dish: AdminDishDto) {
    resetDishEditImage();
    setDishEditForm({
      dishId: dish.dishId,
      name: dish.name,
      price: String(dish.price),
      categoryId: String(dish.categoryId),
      description: dish.description ?? "",
      unit: dish.unit ?? "dia",
      image: dish.image ?? "",
      isVegetarian: dish.isVegetarian,
      isDailySpecial: dish.isDailySpecial,
      available: dish.available,
      isActive: dish.isActive,
    });
    navigate("/Admin/Dishes/Edit");
  }

  async function saveNewDish() {
    if (dishSaving) return;
    if (!dishForm.name.trim() || !dishForm.categoryId) {
      setError("Vui lòng nhập tên món và chọn danh mục.");
      return;
    }

    const payload = {
      name: dishForm.name.trim(),
      price: Number(dishForm.price || "0"),
      categoryId: Number(dishForm.categoryId),
      description: dishForm.description.trim(),
      unit: dishForm.unit.trim() || "dia",
      image: null,
      isVegetarian: dishForm.isVegetarian,
      isDailySpecial: dishForm.isDailySpecial,
      available: dishForm.available,
      isActive: true,
    };

    setDishSaving(true);
    try {
      await refreshAndShow(dishCreateImageFile ? adminApi.createDishWithImage(payload, dishCreateImageFile) : adminApi.createDish(payload));
      setDishForm({ ...emptyDishForm, categoryId: String(dishes?.categories[0]?.categoryId ?? "") });
      resetDishCreateImage();
      navigate("/Admin/Dishes/Index");
    } finally {
      setDishSaving(false);
    }
  }

  async function saveEditedDish() {
    if (dishSaving) return;
    if (!dishEditForm.name.trim() || !dishEditForm.categoryId) {
      setError("Vui lòng nhập tên món và chọn danh mục.");
      return;
    }

    const payload = {
      name: dishEditForm.name.trim(),
      price: Number(dishEditForm.price || "0"),
      categoryId: Number(dishEditForm.categoryId),
      description: dishEditForm.description.trim(),
      unit: dishEditForm.unit.trim() || "dia",
      image: dishEditForm.image || null,
      isVegetarian: dishEditForm.isVegetarian,
      isDailySpecial: dishEditForm.isDailySpecial,
      available: dishEditForm.available,
      isActive: dishEditForm.isActive,
    };

    setDishSaving(true);
    try {
      await refreshAndShow(dishEditImageFile ? adminApi.updateDishWithImage(dishEditForm.dishId, payload, dishEditImageFile) : adminApi.updateDish(dishEditForm.dishId, payload));
      setDishEditForm({ dishId: 0, name: "", price: "10000", categoryId: "", description: "", unit: "dia", image: "", isVegetarian: false, isDailySpecial: false, available: true, isActive: true });
      resetDishEditImage();
      navigate("/Admin/Dishes/Index");
    } finally {
      setDishSaving(false);
    }
  }

  async function openDishIngredients(dishId: number, dishName: string) {
    try {
      setError(null);
      const recipe = await adminApi.getDishIngredients(dishId);
      setDishIngredientPage(1);
      setDishIngredientEditor({ dishId: recipe.dishId, dishName: recipe.dishName || dishName, items: recipe.ingredients });
      navigate(`/Admin/Dishes/Ingredients?dishId=${recipe.dishId}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải nguyên liệu món ăn.");
    }
  }

  async function saveDishIngredientsEditor() {
    if (!dishIngredientEditor) return;
    const items = dishIngredientEditor.items
      .filter((item) => item.selected)
      .map((item) => ({ ingredientId: item.ingredientId, quantityPerDish: item.quantityPerDish }));
    await refreshAndShow(adminApi.saveDishIngredients(dishIngredientEditor.dishId, items));
    setDishIngredientEditor(null);
  }

  async function saveSettings() {
    try {
      const next = await adminApi.updateSettings({
        name: settingsDraft.name.trim(),
        phone: settingsDraft.phone.trim(),
        email: settingsDraft.email.trim() || null,
      });
      setDashboard((current) => current ? { ...current, settings: next } : current);
      showMessage("Đã cập nhật thông tin tài khoản.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật thông tin tài khoản.");
    }
  }

  async function savePasswordChange() {
    try {
      const response = await adminApi.changePassword(passwordEditor);
      setPasswordEditor({ currentPassword: "", newPassword: "", confirmPassword: "" });
      showMessage(response.message);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể đổi mật khẩu.");
    }
  }

  const overviewBranchStats = useMemo(() => {
    const items = tableSummaryItems;
    const occupiedTables = items.filter((table) => table.statusCode === "OCCUPIED").length;
    return {
      occupiedTables,
      availableTables: items.filter((table) => table.statusCode !== "OCCUPIED").length,
      averageSeats: items.length > 0 ? Math.round(items.reduce((sum, table) => sum + table.numberOfSeats, 0) / items.length) : 0,
    };
  }, [tableSummaryItems]);

  const visibleDishes = dishes?.dishes.items ?? [];
  const visibleTables = tablesData?.tables.items ?? [];
  const qrVisibleTables = tableQrFilterId ? visibleTables.filter((table) => table.tableId === Number(tableQrFilterId)) : visibleTables;

  if (loading) return <div className="screen-message">Đang tải khu quản trị...</div>;
  if (error && !dashboard) return <div className="screen-message error-box">{error}</div>;
  if (!dashboard || !categories || !dishes || !tablesData || !reports) return null;

  return (
    <AdminLayout
      title={pageHeading.title}
      description={pageHeading.description}
      staff={staff ?? dashboard.staff}
      onLogout={onLogout}
      onRefresh={loadAll}
      message={message}
      error={error}
    >
      {section === "overview" ? (
        <section className="panel-grid">
          <article className="panel">
            <div className="panel-head">
              <h2>Tổng quan hoạt động</h2>
              <span>{dashboard.staff.branchName}</span>
            </div>
            <div className="list-grid compact-grid">
              <div className="list-card"><strong>{dashboard.stats.activeEmployees}</strong><p>Nhân viên đang hoạt động</p></div>
              <div className="list-card"><strong>{dashboard.stats.pendingOrders}</strong><p>Đơn đang chờ/đang làm</p></div>
              <div className="list-card"><strong>{dashboard.categories.length}</strong><p>Danh mục món ăn</p></div>
              <div className="list-card"><strong>{dashboard.tableStatuses.length}</strong><p>Trạng thái bàn</p></div>
            </div>
            <div className="summary-chip-grid">
              <article className="summary-chip"><span className="eyebrow">Bàn đang sử dụng</span><strong>{overviewBranchStats.occupiedTables}</strong></article>
              <article className="summary-chip"><span className="eyebrow">Bàn còn trống</span><strong>{overviewBranchStats.availableTables}</strong></article>
              <article className="summary-chip"><span className="eyebrow">Sức chứa trung bình</span><strong>{overviewBranchStats.averageSeats}</strong></article>
              <article className="summary-chip"><span className="eyebrow">Doanh thu hôm nay</span><strong>{dashboard.stats.todayRevenue.toLocaleString("vi-VN")} đ</strong></article>
            </div>
          </article>

          <article className="panel">
            <div className="panel-head">
              <h2>Nhân viên gần đây</h2>
              <span>{dashboard.latestEmployees.length}</span>
            </div>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Tên</th>
                  <th>Vai trò</th>
                  <th>Chi nhánh</th>
                  <th>Tài khoản</th>
                </tr>
              </thead>
              <tbody>
                {dashboard.latestEmployees.map((employee) => (
                  <tr key={employee.employeeId}>
                    <td>{employee.name}</td>
                    <td>{employee.roleName}</td>
                    <td>{employee.branchName}</td>
                    <td>{employee.username}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </article>

          <article className="panel">
            <div className="panel-head">
              <h2>Chi nhánh</h2>
              <span>{dashboard.branches.length}</span>
            </div>
            <div className="list-grid compact-grid">
              {dashboard.branches.map((branch) => (
                <div key={branch.branchId} className="list-card">
                  <strong>{branch.name}</strong>
                  <p>{branch.location || "Chưa cập nhật địa chỉ"}</p>
                  <small>Chi nhánh #{branch.branchId}</small>
                </div>
              ))}
            </div>
          </article>
        </section>
      ) : null}

      {section === "categories" ? (
        <section className="panel">
          {isCategoryUnitsPage ? (
            <>
              <div className="toolbar-card">
                <div>
                  <strong>Quản lý đơn vị</strong>
                  <div className="muted">Đơn vị đang được tổng hợp từ dữ liệu món ăn.</div>
                </div>
                <button className="ghost" onClick={() => navigate("/Admin/Categories/Index")}>Quản lý danh mục</button>
              </div>
              <table className="data-table compact-table">
                <thead>
                  <tr>
                    <th>Đơn vị</th>
                    <th className="text-right">Số món</th>
                  </tr>
                </thead>
                <tbody>
                  {categories.units.length > 0 ? categories.units.map((unit) => (
                    <tr key={unit.unit}>
                      <td><strong>{unit.unit}</strong></td>
                      <td className="text-right">{unit.dishCount}</td>
                    </tr>
                  )) : (
                    <tr><td colSpan={2} className="text-right">Chưa có đơn vị nào.</td></tr>
                  )}
                </tbody>
              </table>
            </>
          ) : null}

          {isCategoryStatusesPage ? (
            <>
              <div className="toolbar-card">
                <div>
                  <strong>Quản lý trạng thái</strong>
                  <div className="muted">Trạng thái hiện có đang được dùng trong quản lý bàn.</div>
                </div>
                <button className="ghost" onClick={() => navigate("/Admin/TablesQR/Index")}>Quản lý bàn</button>
              </div>
              <table className="data-table compact-table">
                <thead>
                  <tr>
                    <th>Mã trạng thái</th>
                    <th>Tên trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {dashboard.tableStatuses.length > 0 ? dashboard.tableStatuses.map((status) => (
                    <tr key={status.statusId}>
                      <td><strong>{status.statusCode}</strong></td>
                      <td>{status.statusName}</td>
                    </tr>
                  )) : (
                    <tr><td colSpan={2} className="text-right">Chưa có trạng thái nào.</td></tr>
                  )}
                </tbody>
              </table>
            </>
          ) : null}

          {isCategoryCreatePage ? (
            <>
              <div className="toolbar-card">
                <div>
                  <strong>Thêm mới danh mục</strong>
                  <div className="muted">Tạo danh mục món ăn mới.</div>
                </div>
                <button className="ghost" onClick={() => navigate("/Admin/Categories/Index")}>Quay lại danh sách</button>
              </div>
              <div className="entry-form-card">
                <div className="entry-form-header"><div><strong>Thông tin danh mục</strong><div className="muted">Nhập tên, mô tả và thứ tự hiển thị.</div></div></div>
                <div className="entry-form-grid">
                  <label>Tên danh mục<input value={categoryForm.name} onChange={(e) => setCategoryForm({ ...categoryForm, name: e.target.value })} /></label>
                  <label>Thứ tự hiển thị<input type="number" value={categoryForm.displayOrder} onChange={(e) => setCategoryForm({ ...categoryForm, displayOrder: e.target.value })} /></label>
                  <label className="full-span">Mô tả<textarea rows={3} value={categoryForm.description} onChange={(e) => setCategoryForm({ ...categoryForm, description: e.target.value })} /></label>
                </div>
                <div className="category-helper">
                  <button type="button" className="ghost category-helper-toggle" onClick={() => setCategoryHelperOpen((open) => !open)}>
                    {categoryHelperOpen ? "▼" : "⌄"} Danh mục hiện có
                  </button>
                  {categoryHelperOpen ? (
                    <div className="category-helper-list">
                      {categories.categories.map((category) => <button type="button" className="ghost" key={category.categoryId} onClick={() => setCategoryForm({ ...categoryForm, name: category.name, description: category.description ?? "", displayOrder: String(category.displayOrder) })}>{category.name}</button>)}
                    </div>
                  ) : null}
                </div>
                <div className="entry-form-actions">
                  <button className="ghost" onClick={() => navigate("/Admin/Categories/Index")}>Hủy</button>
                  <button onClick={() => void handleCreateCategory()}>Thêm mới</button>
                </div>
              </div>
            </>
          ) : null}

          {isCategoryEditPage ? (
            <>
              <div className="toolbar-card">
                <div>
                  <strong>Sửa danh mục</strong>
                  <div className="muted">Cập nhật thông tin danh mục đang chọn.</div>
                </div>
                <button className="ghost" onClick={() => navigate("/Admin/Categories/Index")}>Quay lại danh sách</button>
              </div>
              <div className="entry-form-card edit-form-card">
                {categoryEditForm.categoryId === 0 ? (
                  <div className="empty-report history-empty-card">
                    <i className="bi bi-folder2-open" />
                    <strong>Không tìm thấy danh mục</strong>
                    <div>Danh mục này không còn tồn tại hoặc chưa được tải.</div>
                  </div>
                ) : (
                  <>
                    <div className="entry-form-grid">
                      <label>Tên danh mục<input value={categoryEditForm.name} onChange={(e) => setCategoryEditForm({ ...categoryEditForm, name: e.target.value })} /></label>
                      <label>Thứ tự hiển thị<input type="number" value={categoryEditForm.displayOrder} onChange={(e) => setCategoryEditForm({ ...categoryEditForm, displayOrder: e.target.value })} /></label>
                      <label className="full-span">Mô tả<textarea rows={3} value={categoryEditForm.description} onChange={(e) => setCategoryEditForm({ ...categoryEditForm, description: e.target.value })} /></label>
                    </div>
                    <div className="category-helper">
                      <button type="button" className="ghost category-helper-toggle" onClick={() => setCategoryHelperOpen((open) => !open)}>
                        {categoryHelperOpen ? "▼" : "⌄"} Danh mục hiện có
                      </button>
                      {categoryHelperOpen ? (
                        <div className="category-helper-list">
                          {categories.categories.map((category) => <button type="button" className="ghost" key={category.categoryId} onClick={() => openCategoryEditPage(category)}>{category.name}</button>)}
                        </div>
                      ) : null}
                    </div>
                    <div className="filter-chip-row">
                      <button type="button" className={`ghost ${categoryEditForm.isActive ? "active-toggle" : ""}`} onClick={() => setCategoryEditForm({ ...categoryEditForm, isActive: !categoryEditForm.isActive })}>
                        {categoryEditForm.isActive ? "Hoạt động" : "Ngừng hoạt động"}
                      </button>
                    </div>
                    <div className="entry-form-actions">
                      <button className="ghost" onClick={() => navigate("/Admin/Categories/Index")}>Hủy</button>
                      <button onClick={() => void handleUpdateCategory()}>Lưu thay đổi</button>
                    </div>
                  </>
                )}
              </div>
            </>
          ) : null}

          {isCategoryIndexPage ? (
            <>
              <div className="toolbar-card">
                <div>
                  <strong>Quản lý danh mục món ăn</strong>
                  <div className="muted">Danh sách danh mục món ăn hiện có.</div>
                </div>
                <button className="ghost" onClick={() => navigate("/Admin/Categories/Create")}>Thêm mới</button>
              </div>
              <div className="panel-head"><h2>Danh mục món ăn</h2><span className="status-pill success">{categories.categories.length} danh mục</span></div>
              <table className="data-table">
                <thead><tr><th>Tên danh mục</th><th>Mô tả</th><th>Thứ tự</th><th>Kích hoạt</th><th>Thao tác</th></tr></thead>
                <tbody>
                  {categories.categories.length > 0 ? categories.categories.map((category) => (
                    <tr key={category.categoryId}>
                      <td><strong>{category.name}</strong></td>
                      <td>{category.description || "-"}</td>
                      <td>{category.displayOrder}</td>
                      <td>{category.isActive ? <span className="status-pill success">Đang dùng</span> : <span className="status-pill danger">Ngừng dùng</span>}</td>
                      <td>
                        <div className="button-row wrap">
                          <button className="ghost" onClick={() => openCategoryEditPage(category)}>Sửa</button>
                          {category.isActive ? (
                            <button className="danger" onClick={() => void setCategoryActive(category, false)}>Vô hiệu</button>
                          ) : (
                            <button className="ghost" onClick={() => void setCategoryActive(category, true)}>Bật lại</button>
                          )}
                          <button className="danger" onClick={() => void removeCategory(category)}>Xóa</button>
                        </div>
                      </td>
                    </tr>
                  )) : <tr><td colSpan={5} className="text-right">Chưa có danh mục nào.</td></tr>}
                </tbody>
              </table>
            </>
          ) : null}
        </section>
      ) : null}

      {section === "dishes" ? (
        <section className="panel">
          <div className="toolbar-card">
            <div><strong>Quản lý món ăn</strong><div className="muted">Quản lý món ăn, hình ảnh và thành phần món ăn.</div></div>
            <div className="button-row wrap">
              {(isDishCreatePage || isDishEditPage || isDishIngredientsPage) ? <button className="ghost" onClick={() => navigate("/Admin/Dishes/Index")}>Quay lại danh sách món ăn</button> : null}
              <button className={isDishCreatePage ? "active-toggle" : "ghost"} onClick={() => navigate("/Admin/Dishes/Create")}>Thêm món mới</button>
              <button className={isDishIngredientsPage ? "active-toggle" : "ghost"} onClick={() => navigate("/Admin/Dishes/Ingredients")}>Thành phần món ăn</button>
            </div>
          </div>

          {isDishCreatePage ? (
            <div className="entry-form-card">
              <div className="entry-form-header">
                <div><strong>Thêm món mới</strong><div className="muted">Nhập thông tin món ăn theo form tạo mới.</div></div>
                <button className="ghost" onClick={() => { setDishForm({ ...emptyDishForm, categoryId: String(dishes.categories[0]?.categoryId ?? ""), unit: dishUnits[0]?.name ?? "dia" }); resetDishCreateImage(); navigate("/Admin/Dishes/Index"); }}>Đóng</button>
              </div>
              <div className="entry-form-grid">
                <label>Tên món<input value={dishForm.name} onChange={(e) => setDishForm({ ...dishForm, name: e.target.value })} /></label>
                <label>Giá bán<input type="number" value={dishForm.price} onChange={(e) => setDishForm({ ...dishForm, price: e.target.value })} /></label>
                <label>Danh mục<select value={dishForm.categoryId} onChange={(e) => setDishForm({ ...dishForm, categoryId: e.target.value })}>{dishes.categories.map((category) => <option key={category.categoryId} value={category.categoryId}>{category.name}</option>)}</select></label>
                <label>Đơn vị<select value={dishForm.unit} onChange={(e) => setDishForm({ ...dishForm, unit: e.target.value })} disabled={dishUnits.length === 0}>{renderDishUnitOptions(dishForm.unit)}</select></label>
                <label className="full-span">Mô tả<textarea rows={3} value={dishForm.description} onChange={(e) => setDishForm({ ...dishForm, description: e.target.value })} /></label>
              </div>
              {dishUnits.length === 0 ? <div className="muted">Chưa có đơn vị khả dụng. Hãy tạo hoặc bật lại đơn vị trước khi thêm món.</div> : null}
              <div className="filter-chip-row">
                <button type="button" className={`ghost ${dishForm.isVegetarian ? "active-toggle" : ""}`} onClick={() => setDishForm({ ...dishForm, isVegetarian: !dishForm.isVegetarian })}>Món chay</button>
                <button type="button" className={`ghost ${dishForm.isDailySpecial ? "active-toggle" : ""}`} onClick={() => setDishForm({ ...dishForm, isDailySpecial: !dishForm.isDailySpecial })}>Món trong ngày</button>
                <button type="button" className={`ghost ${dishForm.available ? "active-toggle" : ""}`} onClick={() => setDishForm({ ...dishForm, available: !dishForm.available })}>Đang bán</button>
              </div>
              <div className="dish-image-picker">
                <div className="dish-image-preview">
                  {dishCreateImagePreview ? <img src={dishCreateImagePreview} alt="Xem trước ảnh món ăn" /> : <span>Chưa chọn ảnh</span>}
                </div>
                <div className="dish-image-meta">
                  <label className="ghost dish-image-button">
                    Chọn ảnh
                    <input type="file" accept="image/*" onChange={(event) => handleDishImageChange(event, "create")} />
                  </label>
                  <div className="muted">{dishCreateImageFile ? dishCreateImageFile.name : "Chưa có ảnh được chọn."}</div>
                </div>
              </div>
              <div className="entry-form-actions">
                <span className="muted">Chọn ảnh trước nếu món cần hình minh họa. Món chỉ được lưu khi bấm nút lưu.</span>
                <button disabled={dishSaving} onClick={() => void saveNewDish()}>{dishSaving ? "Đang lưu..." : "Thêm món"}</button>
              </div>
            </div>
          ) : null}

          {!isDishCreatePage && !isDishEditPage && !isDishIngredientsPage ? (
            <div className="inline-filter-card admin-filter-card">
              <div><strong>Bộ lọc món ăn</strong><div className="muted">Lọc theo tên, mô tả, danh mục và món chay.</div></div>
              <div className="admin-filter-form">
                <label className="admin-filter-field admin-filter-field-wide"><span>Tìm kiếm</span><input value={dishSearch} onChange={(e) => { setDishPage(1); setDishSearch(e.target.value); }} placeholder="Tìm theo tên hoặc mô tả..." /></label>
                <label className="admin-filter-field"><span>Danh mục</span><select value={dishCategoryFilter} onChange={(e) => { setDishPage(1); setDishCategoryFilter(e.target.value); }}><option value="ALL">Tất cả danh mục</option>{dishes.categories.map((category) => <option key={category.categoryId} value={category.categoryId}>{category.name}</option>)}</select></label>
                <label className="admin-filter-check"><input type="checkbox" checked={dishOnlyVegetarian} onChange={(e) => { setDishPage(1); setDishOnlyVegetarian(e.target.checked); }} /><span>Chỉ món chay</span></label>
              </div>
              <div className="admin-filter-actions"><button className="ghost" onClick={() => { setDishPage(1); setDishSearch(""); setDishCategoryFilter("ALL"); setDishOnlyVegetarian(false); }}>Xóa bộ lọc</button></div>
            </div>
          ) : null}

          {(dishEditForm.dishId > 0 || isDishEditPage) ? (
            <div className="entry-form-card edit-form-card">
              <div className="entry-form-header">
                <div><strong>Chỉnh sửa món ăn</strong><div className="muted">Cập nhật thông tin món ăn đang chọn.</div></div>
                <button className="ghost" onClick={() => { setDishEditForm({ dishId: 0, name: "", price: "10000", categoryId: "", description: "", unit: "dia", image: "", isVegetarian: false, isDailySpecial: false, available: true, isActive: true }); resetDishEditImage(); navigate("/Admin/Dishes/Index"); }}>Đóng</button>
              </div>
              {dishEditForm.dishId === 0 ? (
                <div className="empty-report history-empty-card">
                  <i className="bi bi-pencil-square" />
                  <strong>Chưa có món ăn đang chỉnh sửa</strong>
                  <div>Hãy quay về danh sách món ăn và chọn một món để sửa.</div>
                </div>
              ) : (
                <>
                  <div className="entry-form-grid">
                    <label>Tên món<input value={dishEditForm.name} onChange={(e) => setDishEditForm({ ...dishEditForm, name: e.target.value })} /></label>
                    <label>Giá bán<input type="number" value={dishEditForm.price} onChange={(e) => setDishEditForm({ ...dishEditForm, price: e.target.value })} /></label>
                    <label>Danh mục<select value={dishEditForm.categoryId} onChange={(e) => setDishEditForm({ ...dishEditForm, categoryId: e.target.value })}>{dishes.categories.map((category) => <option key={category.categoryId} value={category.categoryId}>{category.name}</option>)}</select></label>
                    <label>Đơn vị<select value={dishEditForm.unit} onChange={(e) => setDishEditForm({ ...dishEditForm, unit: e.target.value })} disabled={dishUnits.length === 0 && !dishEditForm.unit.trim()}>{renderDishUnitOptions(dishEditForm.unit)}</select></label>
                    <label className="full-span">Mô tả<textarea rows={3} value={dishEditForm.description} onChange={(e) => setDishEditForm({ ...dishEditForm, description: e.target.value })} /></label>
                  </div>
                  {dishUnits.length === 0 ? <div className="muted">Chưa có đơn vị khả dụng. Món hiện tại vẫn giữ đơn vị đang lưu nếu có.</div> : null}
                  <div className="filter-chip-row">
                    <button type="button" className={`ghost ${dishEditForm.isVegetarian ? "active-toggle" : ""}`} onClick={() => setDishEditForm({ ...dishEditForm, isVegetarian: !dishEditForm.isVegetarian })}>Món chay</button>
                    <button type="button" className={`ghost ${dishEditForm.isDailySpecial ? "active-toggle" : ""}`} onClick={() => setDishEditForm({ ...dishEditForm, isDailySpecial: !dishEditForm.isDailySpecial })}>Món trong ngày</button>
                    <button type="button" className={`ghost ${dishEditForm.available ? "active-toggle" : ""}`} onClick={() => setDishEditForm({ ...dishEditForm, available: !dishEditForm.available })}>Đang bán</button>
                  </div>
                  <div className="dish-image-picker">
                    <div className="dish-image-preview">
                      {(dishEditImagePreview || dishEditForm.image) ? (
                        <img src={dishEditImagePreview || dishEditForm.image} alt="Xem trước ảnh món ăn" />
                      ) : (
                        <span>Chưa có ảnh</span>
                      )}
                    </div>
                    <div className="dish-image-meta">
                      <label className="ghost dish-image-button">
                        Chọn ảnh
                        <input type="file" accept="image/*" onChange={(event) => handleDishImageChange(event, "edit")} />
                      </label>
                      <div className="muted">
                        {dishEditImageFile ? dishEditImageFile.name : (dishEditForm.image ? "Đang dùng ảnh hiện tại." : "Chưa có ảnh hiện tại.")}
                      </div>
                    </div>
                  </div>
                  <div className="entry-form-actions">
                    <span className="muted">Chọn ảnh mới nếu muốn thay đổi hình món. Ảnh chỉ được lưu khi bấm lưu.</span>
                    <div className="button-row wrap">
                      <button
                        className="danger"
                        onClick={() => void removeDish({
                          dishId: dishEditForm.dishId,
                          name: dishEditForm.name,
                          price: Number(dishEditForm.price || "0"),
                          categoryId: Number(dishEditForm.categoryId || "0"),
                          categoryName: "",
                          description: dishEditForm.description,
                          unit: dishEditForm.unit,
                          image: dishEditForm.image,
                          isVegetarian: dishEditForm.isVegetarian,
                          isDailySpecial: dishEditForm.isDailySpecial,
                          available: dishEditForm.available,
                          isActive: dishEditForm.isActive,
                        })}
                      >
                        Xóa
                      </button>
                      <button disabled={dishSaving} onClick={() => void saveEditedDish()}>{dishSaving ? "Đang lưu..." : "Lưu thay đổi"}</button>
                    </div>
                  </div>
                </>
              )}
            </div>
          ) : null}

          {isDishIngredientsPage ? (
            dishIngredientEditor ? (
              <div className="panel">
                <div className="panel-head">
                  <div><h2>Thành phần món ăn</h2><p className="muted">{dishIngredientEditor.dishName}</p></div>
                  <button className="ghost" onClick={() => { setDishIngredientEditor(null); navigate("/Admin/Dishes/Index"); }}>Quay lại danh sách món ăn</button>
                </div>
                <div className="dish-ingredient-pagination-summary">
                  <div>
                    <strong>{dishIngredientTotalItems} nguyên liệu</strong>
                    <span className="muted">Trang {dishIngredientPage}/{dishIngredientTotalPages}</span>
                  </div>
                  <label>
                    <span>Hiển thị</span>
                    <select
                      value={dishIngredientPageSize}
                      onChange={(e) => {
                        setDishIngredientPageSize(Number(e.target.value) as (typeof DISH_INGREDIENT_PAGE_SIZE_OPTIONS)[number]);
                        setDishIngredientPage(1);
                      }}
                    >
                      {DISH_INGREDIENT_PAGE_SIZE_OPTIONS.map((option) => <option key={option} value={option}>{option}</option>)}
                    </select>
                  </label>
                </div>
                {dishIngredientTotalItems === 0 ? (
                  <div className="empty-report history-empty-card">
                    <i className="bi bi-basket3-fill" />
                    <strong>Ch?a c? nguy?n li?u cho m?n n?y.</strong>
                  </div>
                ) : (
                <div className="ingredient-modal-list">
                  {pagedDishIngredientItems.map((item, pageIndex) => {
                    const index = dishIngredientPageStart + pageIndex;
                    return (
                    <div key={item.ingredientId} className={`ingredient-line ${item.selected ? "selected" : ""}`}>
                      <label className="ingredient-toggle">
                        <input
                          type="checkbox"
                          checked={item.selected}
                          onChange={(e) => {
                            const next = [...dishIngredientEditor.items];
                            next[index] = { ...item, selected: e.target.checked, quantityPerDish: e.target.checked ? (item.quantityPerDish || 1) : 0 };
                            setDishIngredientEditor({ ...dishIngredientEditor, items: next });
                          }}
                        />
                        <span>
                          <strong>{item.name}</strong>
                          <small className="muted-caption">{item.unit} | Tồn {item.currentStock}</small>
                        </span>
                      </label>
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        value={item.selected ? item.quantityPerDish : 0}
                        disabled={!item.selected}
                        onChange={(e) => {
                          const next = [...dishIngredientEditor.items];
                          next[index] = { ...item, quantityPerDish: Number(e.target.value) };
                          setDishIngredientEditor({ ...dishIngredientEditor, items: next });
                        }}
                      />
                    </div>
                    );
                  })}
                </div>
                )}
                <div className="button-row wrap admin-pagination dish-ingredient-pagination" aria-label="Phân trang nguyên liệu">
                  <button type="button" className="ghost admin-pagination-nav" disabled={dishIngredientPage <= 1} onClick={() => setDishIngredientPage(1)}>Đầu</button>
                  <button type="button" className="ghost admin-pagination-nav" disabled={dishIngredientPage <= 1} onClick={() => setDishIngredientPage((page) => Math.max(1, page - 1))}>Trước</button>
                  <span className="dish-ingredient-pagination-info">
                    {dishIngredientTotalItems === 0 ? "0" : `${dishIngredientPageStart + 1}-${Math.min(dishIngredientPageStart + dishIngredientPageSize, dishIngredientTotalItems)}`} / {dishIngredientTotalItems}
                  </span>
                  <button type="button" className="ghost admin-pagination-nav" disabled={dishIngredientPage >= dishIngredientTotalPages} onClick={() => setDishIngredientPage((page) => Math.min(dishIngredientTotalPages, page + 1))}>Sau</button>
                  <button type="button" className="ghost admin-pagination-nav" disabled={dishIngredientPage >= dishIngredientTotalPages} onClick={() => setDishIngredientPage(dishIngredientTotalPages)}>Cuối</button>
                </div>
                <div className="button-row">
                  <button className="ghost" onClick={() => { setDishIngredientEditor(null); navigate("/Admin/Dishes/Index"); }}>Hủy</button>
                  <button onClick={() => void saveDishIngredientsEditor().then(() => navigate("/Admin/Dishes/Index"))}>Lưu thành phần</button>
                </div>
              </div>
            ) : (
              <div className="empty-report history-empty-card">
                <i className="bi bi-basket3-fill" />
                <strong>Chưa có món ăn đang mở phần thành phần</strong>
                  <div>Hãy quay lại danh sách món ăn và chọn một món để mở phần nguyên liệu.</div>
              </div>
            )
          ) : null}

          {!isDishCreatePage && !isDishEditPage && !isDishIngredientsPage ? (
            <>
              <div className="panel-head"><h2>Danh sách món ăn</h2><span className="status-pill success">{dishes.dishes.totalItems} món</span></div>
              <table className="data-table">
                <thead><tr><th>Hình</th><th>Tên món</th><th>Danh mục</th><th>Đơn vị</th><th>Nguyên liệu</th><th>Giá</th><th>Tình trạng</th><th>Thao tác</th></tr></thead>
                <tbody>
                  {visibleDishes.length > 0 ? visibleDishes.map((dish) => (
                    <tr key={dish.dishId}>
                      <td><img className="thumb" src={dish.image || "/images/placeholder-dish.svg"} alt={dish.name} /></td>
                      <td><strong>{dish.name}</strong><div className="muted">{dish.description || "Chưa có mô tả"}</div></td>
                      <td><span className="status-pill info">{dish.categoryName}</span></td>
                      <td>{dish.unit?.trim() || "-"}</td>
                      <td className="ingredient-summary-cell">{dish.ingredientsSummary || "-"}</td>
                      <td>{dish.price.toLocaleString("vi-VN")} đ</td>
                      <td>{dish.available ? <span className="status-pill success">Đang bán</span> : <span className="status-pill warning">Tạm ngưng</span>}</td>
                      <td>
                        <div className="button-row wrap">
                          <button className="ghost" onClick={() => openDishEditPage(dish)}>Sửa</button>
                          {dish.available ? (
                            <button className="warning" onClick={() => void refreshAndShow(adminApi.setDishAvailability(dish.dishId, false), "Đã tạm ngưng món ăn.")}>Tạm ngưng</button>
                          ) : (
                            <button className="ghost" onClick={() => void refreshAndShow(adminApi.setDishAvailability(dish.dishId, true), "Đã tiếp tục bán món ăn.")}>Tiếp tục</button>
                          )}
                          <button className="ghost" onClick={() => void openDishIngredients(dish.dishId, dish.name)}>Nguyên liệu</button>
                          <button className="danger" onClick={() => void removeDish(dish)}>Xóa</button>
                        </div>
                      </td>
                    </tr>
                  )) : <tr><td colSpan={8} className="text-right">Chưa có món ăn phù hợp với bộ lọc hiện tại.</td></tr>}
                </tbody>
              </table>
              <AdminPagination currentPage={dishPage} totalPages={dishes.dishes.totalPages} onPageChange={setDishPage} keyPrefix="dish" />
            </>
          ) : null}
        </section>
      ) : null}

      {section === "tables" ? (
        <section className="panel">
          <div className="toolbar-card">
            <div><strong>{"Qu\u1ea3n l\u00fd b\u00e0n & m\u00e3 QR"}</strong><div className="muted">{"Qu\u1ea3n l\u00fd b\u00e0n \u0103n v\u00e0 m\u00e3 QR."}</div></div>
            <div className="button-row wrap">
              {(isTableEditPage || isTableQrPage) ? <button className="ghost" onClick={() => navigate("/Admin/TablesQR/Index")}>{"Quay l\u1ea1i danh s\u00e1ch b\u00e0n"}</button> : null}
              <button className={isTableEditPage ? "active-toggle" : "ghost"} onClick={() => navigate("/Admin/TablesQR/Edit")}>{"S\u1eeda b\u00e0n"}</button>
              <button className={isTableQrPage ? "active-toggle" : "ghost"} onClick={() => navigate("/Admin/TablesQR/QR")}>{"M\u00e3 QR"}</button>
            </div>
          </div>

          {!isTableEditPage && !isTableQrPage ? (
            <>
              <div className="entry-form-card">
                <div className="entry-form-header"><div><strong>{"Th\u00eam b\u00e0n m\u1edbi"}</strong><div className="muted">{"Ch\u1ecdn chi nh\u00e1nh, s\u1ed1 gh\u1ebf v\u00e0 tr\u1ea1ng th\u00e1i."}</div></div></div>
                <div className="entry-form-grid">
                  <label>{"Chi nh\u00e1nh"}<select value={tableForm.branchId} onChange={(e) => setTableForm({ ...tableForm, branchId: e.target.value })} disabled>{tablesData.branches.map((branch) => <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>)}</select></label>
                  <label>{"S\u1ed1 gh\u1ebf"}<input type="number" value={tableForm.numberOfSeats} onChange={(e) => setTableForm({ ...tableForm, numberOfSeats: e.target.value })} /></label>
                  <label>{"Tr\u1ea1ng th\u00e1i"}<select value={tableForm.statusId} onChange={(e) => setTableForm({ ...tableForm, statusId: e.target.value })}>{tablesData.tableStatuses.map((status) => <option key={status.statusId} value={status.statusId}>{status.statusName}</option>)}</select></label>
                </div>
                <div className="entry-form-actions">
                  <span className="muted">{"Th\u00eam b\u00e0n m\u1edbi cho chi nh\u00e1nh \u0111ang qu\u1ea3n l\u00fd."}</span>
                  <button onClick={() => {
                    if (!tableForm.branchId || !tableForm.statusId) {
                      setError("Vui l\u00f2ng ch\u1ecdn chi nh\u00e1nh v\u00e0 tr\u1ea1ng th\u00e1i b\u00e0n.");
                      return;
                    }
                    void refreshAndShow(adminApi.createTable({
                      branchId: Number(tableForm.branchId),
                      numberOfSeats: Number(tableForm.numberOfSeats || "4"),
                      statusId: Number(tableForm.statusId),
                      isActive: true,
                    })).then(() => setTableForm({
                      branchId: String(tablesData.branches[0]?.branchId ?? ""),
                      numberOfSeats: "4",
                      statusId: String(tablesData.tableStatuses[0]?.statusId ?? ""),
                    }));
                  }}>{"Th\u00eam b\u00e0n"}</button>
                </div>
              </div>

              <div className="inline-filter-card admin-filter-card">
                <div><strong>{"B\u1ed9 l\u1ecdc b\u00e0n \u0103n"}</strong><div className="muted">{"T\u00ecm theo chi nh\u00e1nh, b\u00e0n, s\u1ed1 gh\u1ebf ho\u1eb7c tr\u1ea1ng th\u00e1i."}</div></div>
                <div className="admin-filter-form">
                  <label className="admin-filter-field admin-filter-field-wide"><span>{"T\u00ecm ki\u1ebfm"}</span><input value={tableSearch} onChange={(e) => { setTablePage(1); setTableSearch(e.target.value); }} placeholder={"T\u00ean chi nh\u00e1nh, s\u1ed1 b\u00e0n, tr\u1ea1ng th\u00e1i..."} /></label>
                  <label className="admin-filter-field"><span>{"Chi nh\u00e1nh"}</span><select value={tableBranchFilter} onChange={(e) => { setTablePage(1); setTableBranchFilter(e.target.value); }} disabled={tablesData.branches.length <= 1}>{tablesData.branches.map((branch) => <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>)}</select></label>
                </div>
                <div className="admin-filter-actions"><button className="ghost" onClick={() => { setTablePage(1); setTableSearch(""); setTableBranchFilter(String(staff?.branchId ?? tablesData.branches[0]?.branchId ?? "")); }}>{"X\u00f3a b\u1ed9 l\u1ecdc"}</button></div>
              </div>
            </>
          ) : null}

          {(tableEditForm.tableId > 0 || isTableEditPage) ? (
            <div className="entry-form-card edit-form-card">
              <div className="entry-form-header">
                <div><strong>{"Ch\u1ec9nh s\u1eeda b\u00e0n"}</strong><div className="muted">{"C\u1eadp nh\u1eadt b\u00e0n \u0111ang ch\u1ecdn."}</div></div>
                <button className="ghost" onClick={() => { setTableEditForm({ tableId: 0, branchId: "", numberOfSeats: "4", statusId: "", qrCode: "", isActive: true }); navigate("/Admin/TablesQR/Index"); }}>{"\u0110\u00f3ng"}</button>
              </div>
              {tableEditForm.tableId === 0 ? (
                <div className="empty-report history-empty-card">
                  <i className="bi bi-grid-3x3-gap-fill" />
                  <strong>{"Ch\u01b0a c\u00f3 b\u00e0n \u0111ang ch\u1ec9nh s\u1eeda"}</strong>
                  <div>{"H\u00e3y ch\u1ecdn m\u1ed9t b\u00e0n t\u1eeb danh s\u00e1ch \u0111\u1ec3 m\u1edf bi\u1ec3u m\u1eabu ch\u1ec9nh s\u1eeda."}</div>
                </div>
              ) : (
                <>
                  <div className="entry-form-grid">
                    <label>{"Chi nh\u00e1nh"}<select value={tableEditForm.branchId} onChange={(e) => setTableEditForm({ ...tableEditForm, branchId: e.target.value })} disabled>{tablesData.branches.map((branch) => <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>)}</select></label>
                    <label>{"S\u1ed1 gh\u1ebf"}<input type="number" value={tableEditForm.numberOfSeats} onChange={(e) => setTableEditForm({ ...tableEditForm, numberOfSeats: e.target.value })} /></label>
                    <label>{"Tr\u1ea1ng th\u00e1i"}<select value={tableEditForm.statusId} onChange={(e) => setTableEditForm({ ...tableEditForm, statusId: e.target.value })}>{tablesData.tableStatuses.map((status) => <option key={status.statusId} value={status.statusId}>{status.statusName}</option>)}</select></label>
                    <label className="full-span">{"M\u00e3 QR"}<input value={tableEditForm.qrCode} readOnly /></label>
                  </div>
                  <div className="filter-chip-row">
                    <button type="button" className={`ghost ${tableEditForm.isActive ? "active-toggle" : ""}`} onClick={() => setTableEditForm({ ...tableEditForm, isActive: !tableEditForm.isActive })}>
                      {tableEditForm.isActive ? "Hiển thị" : "Đã ẩn"}
                    </button>
                  </div>
                  <div className="entry-form-actions">
                    <span className="muted">{"Gi\u1eef \u0111\u00fang lu\u1ed3ng ch\u1ec9nh s\u1eeda b\u00e0n v\u00e0 m\u00e3 QR."}</span>
                    <button onClick={() => {
                      if (!tableEditForm.branchId || !tableEditForm.statusId) {
                        setError("Vui l\u00f2ng ch\u1ecdn chi nh\u00e1nh v\u00e0 tr\u1ea1ng th\u00e1i b\u00e0n.");
                        return;
                      }
                      void refreshAndShow(adminApi.updateTable(tableEditForm.tableId, {
                        branchId: Number(tableEditForm.branchId),
                        numberOfSeats: Number(tableEditForm.numberOfSeats || "4"),
                        statusId: Number(tableEditForm.statusId),
                        isActive: tableEditForm.isActive,
                      })).then(() => {
                        setTableEditForm({ tableId: 0, branchId: "", numberOfSeats: "4", statusId: "", qrCode: "", isActive: true });
                        navigate("/Admin/TablesQR/Index");
                      });
                    }}>{"L\u01b0u thay \u0111\u1ed5i"}</button>
                  </div>
                </>
              )}
            </div>
          ) : null}

          {isTableQrPage ? (
            <>
              <div className="panel-head">
                <div>
                  <h2>Danh sách mã QR bàn</h2>
                  <p className="muted">Mã QR dẫn khách đến giao diện gọi món theo đúng bàn.</p>
                </div>
                <span className="status-pill success">{tableQrFilterId ? qrVisibleTables.length : tablesData.tables.totalItems} bàn</span>
              </div>
              <div className="qr-card-grid">
                {qrVisibleTables.length > 0 ? qrVisibleTables.map((table) => {
                  const qrUrl = buildQrTargetUrl(table.qrCode);
                  const qrImageUrl = buildQrImageUrl(table.qrCode);
                  return (
                    <article key={`qr-${table.tableId}`} className="qr-table-card">
                      <div className="qr-table-card-head">
                        <div>
                          <h3>Bàn {table.tableNumber}</h3>
                          <p>{table.branchName}</p>
                        </div>
                        <span className="status-pill info">{table.statusName}</span>
                      </div>
                      <div className="qr-table-card-body">
                        {qrImageUrl ? <img className="qr-preview" src={qrImageUrl} alt={`QR bàn ${table.tableNumber}`} /> : <div className="empty-report compact-empty">Chưa có mã QR</div>}
                        <div className="qr-url-box">{qrUrl || "Chưa có URL QR"}</div>
                      </div>
                      <div className="button-row wrap">
                        <button className="ghost" disabled={!qrUrl} onClick={() => void navigator.clipboard?.writeText(qrUrl)}>Copy URL</button>
                        <a className="ghost button-link" href={qrImageUrl || undefined} download={`ban-${table.tableNumber}-qr.png`} aria-disabled={!qrImageUrl}>Tải QR</a>
                        <button className="ghost" disabled={!qrImageUrl} onClick={() => window.open(qrImageUrl, "_blank", "noopener,noreferrer")}>In / mở QR</button>
                        <button className="ghost" onClick={() => navigate(`/Admin/TablesQR/Edit/${table.tableId}`)}>Sửa bàn</button>
                      </div>
                    </article>
                  );
                }) : (
                  <div className="empty-report history-empty-card">
                    <i className="bi bi-qr-code" />
                    <strong>Chưa có bàn phù hợp</strong>
                    <div>Không tìm thấy bàn để hiển thị mã QR. Hãy kiểm tra bộ lọc hoặc danh sách bàn.</div>
                  </div>
                )}
              </div>
              {!tableQrFilterId ? <AdminPagination currentPage={tablePage} totalPages={tablesData.tables.totalPages} onPageChange={setTablePage} keyPrefix="table-qr" /> : null}
            </>
          ) : null}

          {!isTableEditPage && !isTableQrPage ? (
            <>
              <div className="panel-head"><h2>{"Danh s\u00e1ch b\u00e0n \u0103n"}</h2><span className="status-pill success">{tablesData.tables.totalItems} {"b\u00e0n"}</span></div>
              <table className="data-table">
                <thead><tr><th>{"B\u00e0n"}</th><th>{"Chi nh\u00e1nh"}</th><th>{"S\u1ed1 gh\u1ebf"}</th><th>{"Tr\u1ea1ng th\u00e1i"}</th><th>QR</th><th>{"Thao t\u00e1c"}</th></tr></thead>
                <tbody>
                  {visibleTables.length > 0 ? visibleTables.map((table) => (
                    <tr key={table.tableId}>
                      <td><strong>{"B\u00e0n"} {table.tableNumber}</strong></td>
                      <td>{table.branchName}</td>
                      <td>{table.numberOfSeats}</td>
                      <td><span className="status-pill info">{table.statusName}</span></td>
                      <td>{table.qrCode || "-"}</td>
                      <td>
                        <div className="button-row wrap">
                          <button className="ghost" onClick={() => openTableEditPage(table)}>{"S\u1eeda"}</button>
                          <button className="ghost" onClick={() => navigate("/Admin/TablesQR/QR")}>QR</button>
                          {table.isActive ? (
                            <button className="danger" onClick={() => void refreshAndShow(adminApi.deactivateTable(table.tableId), "Đã ẩn bàn.")}>Ẩn bàn</button>
                          ) : (
                            <button className="ghost" onClick={() => void setTableActive(table, true)}>{"Hiển thị lại"}</button>
                          )}
                          <button className="danger" onClick={() => void removeTable(table)}>Xóa</button>
                        </div>
                      </td>
                    </tr>
                  )) : <tr><td colSpan={6} className="text-right">{"Ch\u01b0a c\u00f3 b\u00e0n ph\u00f9 h\u1ee3p v\u1edbi b\u1ed9 l\u1ecdc hi\u1ec7n t\u1ea1i."}</td></tr>}
                </tbody>
              </table>
              <AdminPagination currentPage={tablePage} totalPages={tablesData.tables.totalPages} onPageChange={setTablePage} keyPrefix="table" />
            </>
          ) : null}
        </section>
      ) : null}

      {section === "reports" ? (
        <section className="panel-grid">
          {isRevenuePage ? (
            <article className="panel">
              <div className="toolbar-card">
                <div><strong>Báo cáo doanh thu</strong><div className="muted">Tổng quan doanh thu theo ngày và chi nhánh.</div></div>
                <div className="button-row wrap">
                  <button className={isRevenuePage ? "active-toggle" : "ghost"} onClick={() => navigate("/Admin/Reports/Revenue")}>Doanh thu</button>
                  <button className={isTopDishesPage ? "active-toggle" : "ghost"} onClick={() => navigate("/Admin/Reports/TopDishes")}>Top món ăn</button>
                </div>
              </div>
              <div className="history-filter-shell">
                <div className="history-filter-tabs">
                  {reportBranchOptions.map((option) => (
                    <button key={option.key} className={`history-filter-tab ${reportBranchFilter === option.key ? "active" : ""}`} onClick={() => setReportBranchFilter(option.key)}>
                      {option.label} <span>{option.count}</span>
                    </button>
                  ))}
                </div>
              </div>
              <div className="panel-head"><h2>Báo cáo doanh thu</h2><span>{filteredRevenueTotal.toLocaleString("vi-VN")} đ</span></div>
              {filteredRevenueRows.length === 0 ? (
                <div className="empty-report"><i className="bi bi-graph-up-arrow" /><p>Chưa có dữ liệu doanh thu.</p></div>
              ) : (
                <table className="data-table">
                  <thead><tr><th>Ngày</th><th>Chi nhánh</th><th>Số đơn</th><th>Doanh thu</th></tr></thead>
                  <tbody>
                    {filteredRevenueRows.map((row, index) => (
                      <tr key={`${row.branchId}-${row.date}-${index}`}>
                        <td>{row.date}</td>
                        <td>{row.branchName}</td>
                        <td>{row.totalOrders}</td>
                        <td>{row.totalRevenue.toLocaleString("vi-VN")} đ</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </article>
          ) : null}

          {isTopDishesPage ? (
            <article className="panel">
              <div className="toolbar-card">
                <div><strong>Món ăn được gọi nhiều nhất</strong><div className="muted">Top món theo số lượng bán ra.</div></div>
                <div className="button-row wrap"><button className="ghost" onClick={() => navigate("/Admin/Reports/Revenue")}>Quay lại báo cáo doanh thu</button></div>
              </div>
              <table className="data-table">
                <thead><tr><th>#</th><th>Món ăn</th><th>Danh mục</th><th>Số lượng</th><th>Doanh thu</th></tr></thead>
                <tbody>
                  {reports.topDishes.items.map((item, index) => (
                    <tr key={item.dishId}>
                      <td>{index + 1}</td>
                      <td><strong>{item.dishName}</strong></td>
                      <td>{item.categoryName}</td>
                      <td>{item.totalQuantity}</td>
                      <td>{item.totalRevenue.toLocaleString("vi-VN")} đ</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </article>
          ) : null}
        </section>
      ) : null}

      {section === "settings" ? (
        <section className="panel">
          <div className="panel-head">
            <div><h2>Cài đặt tài khoản</h2><p className="muted">Cập nhật thông tin cá nhân và mật khẩu.</p></div>
            <span className="status-pill info"><i className="bi bi-person-badge-fill" /> {dashboard.settings.username}</span>
          </div>

          <section className="entry-form-card settings-form-card">
            <div className="entry-form-header">
              <div><strong>Thông tin liên hệ</strong><div className="muted">{dashboard.settings.branchName} | {dashboard.settings.roleName}</div></div>
            </div>
            <div className="entry-form-grid">
              <label>Tên đăng nhập<input value={dashboard.settings.username} readOnly /></label>
              <label>Họ tên<input value={settingsDraft.name} onChange={(e) => setSettingsDraft({ ...settingsDraft, name: e.target.value })} /></label>
              <label>Số điện thoại<input value={settingsDraft.phone} onChange={(e) => setSettingsDraft({ ...settingsDraft, phone: e.target.value })} /></label>
              <label className="full-span">Email<input value={settingsDraft.email} onChange={(e) => setSettingsDraft({ ...settingsDraft, email: e.target.value })} /></label>
            </div>
            <div className="entry-form-actions">
              <span className="muted">Cập nhật đúng thông tin tài khoản quản trị.</span>
              <div className="button-row wrap">
                <button className="ghost" onClick={() => setSettingsDraft({ name: dashboard.settings.name, phone: dashboard.settings.phone ?? "", email: dashboard.settings.email ?? "" })}>Đặt lại</button>
                <button onClick={() => void saveSettings()}>Lưu thay đổi</button>
              </div>
            </div>
          </section>

          <section className="entry-form-card settings-security-card">
            <div className="entry-form-header">
              <div><strong>Đổi mật khẩu</strong><div className="muted">Nếu không muốn đổi mật khẩu, hãy để trống các ô bên dưới.</div></div>
              <span className="status-pill warning"><i className="bi bi-key-fill" /> Mật khẩu quản trị</span>
            </div>
            <div className="entry-form-grid">
              <label>Mật khẩu hiện tại<input type="password" value={passwordEditor.currentPassword} onChange={(e) => setPasswordEditor({ ...passwordEditor, currentPassword: e.target.value })} /></label>
              <label>Mật khẩu mới<input type="password" value={passwordEditor.newPassword} onChange={(e) => setPasswordEditor({ ...passwordEditor, newPassword: e.target.value })} /></label>
              <label className="full-span">Nhập lại mật khẩu mới<input type="password" value={passwordEditor.confirmPassword} onChange={(e) => setPasswordEditor({ ...passwordEditor, confirmPassword: e.target.value })} /></label>
            </div>
            <div className="entry-form-actions">
              <div className="muted">Nếu không muốn đổi mật khẩu, hãy để trống các ô bên trên.</div>
              <div className="button-row wrap">
                <button className="ghost" onClick={() => setPasswordEditor({ currentPassword: "", newPassword: "", confirmPassword: "" })}>Hủy nhập</button>
                <button className="ghost" onClick={() => void onLogout()}>Đăng xuất</button>
                <button onClick={() => void savePasswordChange()}>Lưu mật khẩu</button>
              </div>
            </div>
          </section>
        </section>
      ) : null}
      <Dialog />
    </AdminLayout>
  );
}
