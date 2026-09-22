import { EmployeesModulePage } from "./EmployeesModulePage";

type Props = { onLogout: () => Promise<void> };

export function EmployeesIndexPage({ onLogout }: Props) {
  return <EmployeesModulePage mode="index" onLogout={onLogout} />;
}
