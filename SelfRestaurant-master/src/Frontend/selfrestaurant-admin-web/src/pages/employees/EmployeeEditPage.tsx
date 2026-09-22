import { EmployeesModulePage } from "./EmployeesModulePage";

type Props = { onLogout: () => Promise<void> };

export function EmployeeEditPage({ onLogout }: Props) {
  return <EmployeesModulePage mode="edit" onLogout={onLogout} />;
}
