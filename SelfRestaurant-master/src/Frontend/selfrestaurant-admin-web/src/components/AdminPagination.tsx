type Props = {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  keyPrefix: string;
};

function getVisiblePages(currentPage: number, totalPages: number) {
  if (totalPages <= 3) {
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  }

  if (currentPage <= 1) {
    return [1, 2];
  }

  if (currentPage >= totalPages) {
    return [totalPages - 1, totalPages];
  }

  return [currentPage - 1, currentPage, currentPage + 1];
}

export function AdminPagination({ currentPage, totalPages, onPageChange, keyPrefix }: Props) {
  if (totalPages <= 1) return null;

  const visiblePages = getVisiblePages(currentPage, totalPages);

  return (
    <div className="button-row wrap admin-pagination" aria-label="Phân trang">
      {currentPage > 1 ? (
        <button
          type="button"
          className="ghost admin-pagination-nav"
          onClick={() => onPageChange(currentPage - 1)}
          aria-label="Trang trước"
        >
          {"<"}
        </button>
      ) : null}

      {visiblePages.map((pageNumber) => (
        <button
          key={`${keyPrefix}-page-${pageNumber}`}
          type="button"
          className={pageNumber === currentPage ? "active-toggle admin-pagination-page" : "ghost admin-pagination-page"}
          onClick={() => onPageChange(pageNumber)}
          aria-current={pageNumber === currentPage ? "page" : undefined}
        >
          {pageNumber}
        </button>
      ))}

      {currentPage < totalPages ? (
        <button
          type="button"
          className="ghost admin-pagination-nav"
          onClick={() => onPageChange(currentPage + 1)}
          aria-label="Trang sau"
        >
          {">"}
        </button>
      ) : null}
    </div>
  );
}
