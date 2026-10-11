// Public shells only. API, cookies, hubs and private data remain in CloudHost.
import { execFileSync } from "node:child_process";
import { cpSync, mkdirSync } from "node:fs";
import { join } from "node:path";
const npm = process.platform === "win32" ? "npm.cmd" : "npm";
for (const role of ["chef", "cashier", "admin"]) {
  const root = join(process.cwd(), `selfrestaurant-${role}-web`);
  execFileSync(npm, ["ci"], { cwd: root, stdio: "inherit", shell: process.platform === "win32" });
  execFileSync(npm, ["run", "build"], { cwd: root, stdio: "inherit", shell: process.platform === "win32" });
  const target = join(process.cwd(), "staff-public", "app", role);
  mkdirSync(target, { recursive: true });
  cpSync(join(root, "dist"), target, { recursive: true });
}
