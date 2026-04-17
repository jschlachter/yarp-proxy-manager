import { test, expect } from "@playwright/test";

/**
 * E2E: Module Registry / Sidebar Navigation
 *
 * Requires:
 * - Next.js dev server running at http://localhost:3000
 * - DEV_AUTH_SUB=dev-admin, DEV_AUTH_GROUPS=proxy-admins set in .env.local
 *
 * These tests verify that the sidebar reflects MODULE_REGISTRY dynamically.
 * The Health Checks module (added in T047) must be enabled in modules.ts.
 */

test.describe("Module registry sidebar", () => {
  test("Routes module appears in sidebar", async ({ page }) => {
    await page.goto("/routes");
    const routesLink = page.locator("nav a", { hasText: "Routes" });
    await expect(routesLink).toBeVisible();
  });

  test("Health Checks module appears in sidebar when enabled", async ({ page }) => {
    await page.goto("/routes");
    const healthChecksLink = page.locator("nav a", { hasText: "Health Checks" });
    await expect(healthChecksLink).toBeVisible();
  });

  test("navigating to Health Checks page works", async ({ page }) => {
    await page.goto("/routes");
    await page.click("nav a:has-text('Health Checks')");
    await expect(page).toHaveURL("/health-checks");
    await expect(page.locator("text=/health checks/i")).toBeVisible();
  });

  test("navigating back to Routes after visiting Health Checks works correctly", async ({
    page,
  }) => {
    await page.goto("/health-checks");
    await page.click("nav a:has-text('Routes')");
    await expect(page).toHaveURL("/routes");
    // Routes page should still load normally
    const hasRoutes =
      (await page.locator("[data-testid='route-card'], .rounded-lg.border").count()) +
      (await page.locator("text=/no routes/i").count());
    expect(hasRoutes).toBeGreaterThan(0);
  });

  test("both Routes and Health Checks nav links are present simultaneously", async ({
    page,
  }) => {
    await page.goto("/routes");
    const navLinks = page.locator("nav a");
    const labels = await navLinks.allTextContents();
    expect(labels).toContain("Routes");
    expect(labels).toContain("Health Checks");
  });
});
