import { Component, type ErrorInfo, type ReactNode } from "react";
import { Link } from "react-router-dom";

type Props = {
  children: ReactNode;
};

type State = {
  hasError: boolean;
  message: string;
};

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, message: "" };
  }

  static getDerivedStateFromError(error: Error): State {
    return {
      hasError: true,
      message: error.message || "Trang hiện tại đã gặp lỗi khi hiển thị.",
    };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error("Customer UI crashed", error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="container py-5">
          <div className="card">
            <h3 className="mb-3">Trang này đang gặp lỗi hiển thị</h3>
            <p className="muted mb-3">
              Mình đã chặn lỗi để trang không còn trắng toàn bộ nữa. Bạn có thể quay về trang chủ hoặc tải lại.
            </p>
            <div className="error-box mb-3">{this.state.message}</div>
            <div className="d-flex flex-wrap gap-2">
              <Link className="btn btn-danger" to="/Home/Index">
                Quay về trang chủ
              </Link>
              <button type="button" className="btn btn-outline-secondary" onClick={() => window.location.reload()}>
                Tải lại trang
              </button>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
