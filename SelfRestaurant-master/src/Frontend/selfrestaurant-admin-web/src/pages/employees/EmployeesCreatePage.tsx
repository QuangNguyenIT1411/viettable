import { EmployeesModulePage } from "./EmployeesModulePage";

type Props = { onLogout: () => Promise<void> };

export function EmployeesCreatePage({ onLogout }: Props) {
  return <EmployeesModulePage mode="create" onLogout={onLogout} />;
}
