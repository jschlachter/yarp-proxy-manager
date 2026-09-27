import path from "node:path";
import { test, expect } from "@playwright/test";

/**
 * E2E: Certificate Upload/List/Delete (Phase 6)
 *
 * Requires:
 * - Next.js dev server running at http://localhost:3000
 * - ProxyManager.API reachable at PROXY_MANAGER_API_URL
 * - ProxyManager.Files reachable at PROXY_MANAGER_FILES_URL, backed by a live RustFS + Postgres
 * - DEV_AUTH_SUB=dev-admin, DEV_AUTH_GROUPS=proxy-admins set in .env.local
 *
 * Uses a real self-signed PEM fixture (tests/e2e/fixtures/test-cert.pem, CN=localhost) so the
 * upload clears the Files service's magic-byte content validation, not just a mocked round trip.
 * Generate it with: openssl req -x509 -newkey rsa:2048 -nodes -days 365 -subj "/CN=localhost"
 *   -keyout /dev/null -out tests/e2e/fixtures/test-cert.pem
 */

const FIXTURE_CERT = path.join(__dirname, "fixtures", "test-cert.pem");

test.describe("Certificate List", () => {
  test("navigates to /certificates and shows the certificate list", async ({ page }) => {
    await page.goto("/certificates");
    const hasCertificates = await page.locator(".rounded-xl.border").count();
    const hasEmptyState = await page.locator("text=/no certificates/i").count();
    expect(hasCertificates + hasEmptyState).toBeGreaterThan(0);
  });
});

test.describe("Upload, list, view, and delete a certificate", () => {
  test("admin can upload a PEM certificate, see it listed, view it, then delete it", async ({ page }) => {
    const certName = `e2e-cert-${Date.now()}`;

    await page.goto("/certificates/new");
    await page.fill("#name", certName);
    await page.selectOption("#format", "Pem");
    await page.setInputFiles("#certificateFile", FIXTURE_CERT);
    await page.click("button:text('Upload Certificate')");

    await expect(page).toHaveURL("/certificates", { timeout: 15_000 });
    await expect(page.locator(`text=${certName}`)).toBeVisible();

    const certCard = page.locator(`.rounded-xl.border:has-text("${certName}")`);

    await certCard.locator("button:text('View')").click();
    const dialog = page.getByRole("dialog");
    await expect(dialog).toContainText(certName);
    await expect(dialog.locator("dt:text('Common name') + dd")).toHaveText("localhost");
    await expect(dialog.locator("dt:text('Expires') + dd time")).not.toBeEmpty();
    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
    await expect(page).toHaveURL("/certificates");
    await expect(certCard).toBeVisible();

    await certCard.locator("button:text('Delete')").click();
    await dialog.getByRole("button", { name: "Delete" }).click();

    await expect(certCard).not.toBeVisible();
  });
});
