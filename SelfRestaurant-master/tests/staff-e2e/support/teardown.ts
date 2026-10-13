import type { FullConfig } from "@playwright/test";
import { settleTestTables } from "./cleanup";

export default async function globalTeardown(config: FullConfig) {
  await settleTestTables(config.projects[0].use.baseURL as string);
}
