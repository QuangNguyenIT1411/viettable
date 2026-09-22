import { CustomersModulePage } from "./CustomersModulePage";

type Props = { onLogout: () => Promise<void> };

export function CustomersCreatePage({ onLogout }: Props) {
  return <CustomersModulePage mode="create" onLogout={onLogout} />;
}
