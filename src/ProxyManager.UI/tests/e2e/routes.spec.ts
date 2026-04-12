import { test, expect } from "@playwright/test";

/**
 * E2E: Proxy Route Management
 *
 * Requires:
 * - Next.js dev server running at http://localhost:3000
 * - ProxyManager API reachable at PROXY_MANAGER_API_URL
 * - DEV_AUTH_SUB=dev-admin, DEV_AUTH_GROUPS=proxy-admins set in .env.local
 */

test.describe("Route List", () => {
  test("navigates to /routes and shows route list", async ({ page }) => {
    await page.goto("/routes");
    // Either a list of routes or an empty state message
    const hasRoutes = await page.locator("[data-testid='route-card'], .rounded-lg.border").count();
    const hasEmptyState = await page.locator("text=/no routes/i").count();
    expect(hasRoutes + hasEmptyState).toBeGreaterThan(0);
  });
});

test.describe("Create Route", () => {
  test("admin can create a new route", async ({ page }) => {
    const routeName = `e2e-test-${Date.now()}`;

    await page.goto("/routes/new");
    await page.fill("label:text('Name') + input, input#name", routeName);
    await page.fill("label:text('Upstream URL') + input, input#upstreamUrl", "http://e2e-backend:9999");
    await page.fill("label:text('Hostnames') + input, input#hostnames", `${routeName}.example.com`);
    await page.click("button:text('Create Route')");

    // Should redirect back to route list after creation
    await expect(page).toHaveURL("/routes");
    await expect(page.locator(`text=${routeName}`)).toBeVisible();
  });
});

test.describe("Edit Route", () => {
  test("admin can edit an existing route and see updated values", async ({ page }) => {
    // Navigate to route list to find an existing route
    await page.goto("/routes");
    const editLink = page.locator("a:text('Edit')").first();
    await editLink.click();

    // Update the route name with a timestamp
    const updatedName = `Updated-${Date.now()}`;
    const nameInput = page.locator("input#name");
    await nameInput.fill(updatedName);
    await page.click("button:text('Save Changes')");

    // Should show updated name
    await expect(page.locator(`text=${updatedName}`)).toBeVisible();
  });
});

test.describe("Delete Route", () => {
  test("admin can delete a route and it disappears from the list", async ({ page }) => {
    // Create a route to delete
    const routeName = `delete-test-${Date.now()}`;
    await page.goto("/routes/new");
    await page.fill("input#name", routeName);
    await page.fill("input#upstreamUrl", "http://delete-me:1234");
    await page.fill("input#hostnames", `${routeName}.example.com`);
    await page.click("button:text('Create Route')");

    await expect(page).toHaveURL("/routes");

    // Find and delete the newly created route
    const routeCard = page.locator(`.rounded-lg.border:has-text("${routeName}")`);
    await expect(routeCard).toBeVisible();

    await routeCard.locator("button:text('Delete')").click();

    // Confirm the deletion dialog
    await page.click("button:text('Delete'):visible");

    // Route should be gone
    await expect(routeCard).not.toBeVisible();
    await expect(page.locator(`text=${routeName}`)).not.toBeVisible();
  });
});
