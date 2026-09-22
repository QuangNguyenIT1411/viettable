import { EmployeesModulePage } from "./EmployeesModulePage";

type Props = { onLogout: () => Promise<void> };

export function EmployeeHistoryPage({ onLogout }: Props) {
  return <EmployeesModulePage mode="history" onLogout={onLogout} />;
}
