import { CustomersModulePage } from "./CustomersModulePage";

type Props = { onLogout: () => Promise<void> };

export function CustomersIndexPage({ onLogout }: Props) {
  return <CustomersModulePage mode="index" onLogout={onLogout} />;
}
