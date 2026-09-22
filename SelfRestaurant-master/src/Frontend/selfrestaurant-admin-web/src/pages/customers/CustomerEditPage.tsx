import { CustomersModulePage } from "./CustomersModulePage";

type Props = { onLogout: () => Promise<void> };

export function CustomerEditPage({ onLogout }: Props) {
  return <CustomersModulePage mode="edit" onLogout={onLogout} />;
}
