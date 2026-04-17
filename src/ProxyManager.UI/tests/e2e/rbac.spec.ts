import { test, expect } from "@playwright/test";

/**
 * E2E: Role-Based Access Control
 *
 * Requires:
 * - Next.js dev server running at http://localhost:3000
 * - ProxyManager API reachable at PROXY_MANAGER_API_URL
 *
 * Admin tests: DEV_AUTH_SUB=dev-admin, DEV_AUTH_GROUPS=proxy-admins set in .env.local
 * Non-admin tests: use a request context that omits the admin group header
 */

test.describe("Admin RBAC", () => {
  test("admin sees Delete button on route list", async ({ page }) => {
    await page.goto("/routes");
    // If there are routes, admin should see Delete button
    const routeCards = page.locator(".rounded-lg.border");
    const count = await routeCards.count();
    if (count > 0) {
      await expect(routeCards.first().locator("button:text('Delete')")).toBeVisible();
    } else {
      // Empty state — admin should see Add Route link
      await expect(page.locator("a:text('Add Route')")).toBeVisible();
    }
  });

  test("admin sees Add Route button on route list", async ({ page }) => {
    await page.goto("/routes");
    await expect(page.locator("a:text('Add Route'), a[href='/routes/new']").first()).toBeVisible();
  });

  test("admin can access POST /api/routes and get non-403 response", async ({ request }) => {
    const response = await request.post("/api/routes", {
      data: {
        name: "rbac-test-route",
        upstreamUrl: "http://rbac-test:8080",
        hostnames: ["rbac-test.example.com"],
        isEnabled: true,
      },
    });
    // Admin should not get 403; may get 201, 409, 422 etc.
    expect(response.status()).not.toBe(403);
  });
});

test.describe("Non-admin RBAC", () => {
  // These tests use a separate browser context without admin headers.
  // In dev mode with DEV_AUTH_SUB set but DEV_AUTH_GROUPS omitting 'proxy-admins',
  // the user is authenticated but not admin.

  test("POST /api/routes returns 403 for non-admin", async ({ request }) => {
    // Send request without admin group header
    const response = await request.post("/api/routes", {
      headers: {
        "X-Auth-Sub": "non-admin-user",
        "X-Auth-Groups": "viewers",
        Authorization: "Bearer non-admin-token",
      },
      data: { name: "test" },
    });
    expect(response.status()).toBe(403);
    const body = await response.json();
    expect(body.status).toBe(403);
  });

  test("non-admin sees read-only form on route detail page", async ({ browser }) => {
    // Create a context simulating a non-admin user
    const context = await browser.newContext({
      extraHTTPHeaders: {
        "X-Auth-Sub": "non-admin-user",
        "X-Auth-Groups": "viewers",
      },
    });
    const page = await context.newPage();

    await page.goto("/routes");
    const editLinks = page.locator("a:text('Edit')");
    const editCount = await editLinks.count();

    if (editCount > 0) {
      // Navigate to first route detail
      await editLinks.first().click();
      // Non-admin should not see editable inputs
      const submitButton = page.locator("button:text('Save Changes')");
      await expect(submitButton).not.toBeVisible();
    }
    // If no edit links visible, non-admin sees no Edit buttons — that's also valid

    await context.close();
  });
});
