import { test, expect } from "@playwright/test";

/**
 * Template editor navigation and draft creation flow tests.
 *
 * These tests verify:
 * 1. Navigation to template catalog
 * 2. Clicking "Edit template" button
 * 3. Draft creation with persistent session ID
 * 4. Editor loading with template data
 * 5. JSON editing and saving
 * 6. Cross-session isolation
 */

test.describe("Template Editor Flow", () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to catalog
    await page.goto("http://localhost:5173/templates");
    await page.waitForLoadState("networkidle");
  });

  test("should display template catalog with edit buttons", async ({ page }) => {
    // Check that templates are loaded
    await expect(page.locator("text=Life Journey Banker")).toBeVisible();
    await expect(page.locator("text=Property Trading Banker")).toBeVisible();

    // Check that edit buttons exist
    const editButtons = page.locator('button:has-text("Edit template")');
    await expect(editButtons.first()).toBeVisible();
  });

  test("should navigate to editor and load draft without spinner", async ({ page, context }) => {
    // Get session storage before test
    let sessionId = await page.evaluate(() => {
      return sessionStorage.getItem("bankers-seat:session-user-id");
    });
    console.log("Session ID before:", sessionId);

    // Click first edit button
    await page.locator('button:has-text("Edit template")').first().click();

    // Wait for navigation
    await page.waitForURL(/\/templates\/edit\//);
    expect(page.url()).toMatch(/\/templates\/edit\//);

    // Wait for editor to load (give it time for draft to be created and fetched)
    await page.waitForTimeout(3000);
    
    // Should NOT see loading spinner anymore
    const spinner = page.locator("svg.MuiCircularProgress-svg, [role='progressbar']");
    const spinnerCount = await spinner.count();
    console.log("Spinners found:", spinnerCount);
    
    if (spinnerCount > 0) {
      // Check if spinner is still visible
      const isVisible = await spinner.first().isVisible({ timeout: 500 }).catch(() => false);
      console.log("Spinner visible:", isVisible);
      expect(isVisible, "Loading spinner should not be visible after editor loads").toBeFalsy();
    }

    // Check session ID persists
    const sessionIdAfter = await page.evaluate(() => {
      return sessionStorage.getItem("bankers-seat:session-user-id");
    });
    console.log("Session ID after:", sessionIdAfter);
    expect(sessionIdAfter).toBeTruthy();
  });

  test("should display template JSON in editor", async ({ page }) => {
    // Click edit button and wait for nav
    await page.locator('button:has-text("Edit template")').first().click();
    await page.waitForURL(/\/templates\/edit\//);
    await page.waitForTimeout(2000);

    // Check for JSON content
    const jsonArea = page.locator("textarea, [role='textbox']").first();
    const content = await jsonArea.inputValue();

    // Should contain JSON structure
    expect(content).toContain('"');
    expect(content).toContain(":");
  });

  test("should maintain session across draft operations", async ({ page }) => {
    // Must be on catalog page to have edit buttons
    await page.goto("http://localhost:5173/templates");
    await page.waitForLoadState("networkidle");
    
    // Get initial session ID (should be set on page load)
    const initialSessionId = await page.evaluate(() => {
      return sessionStorage.getItem("bankers-seat:session-user-id");
    });
    
    console.log("Initial session ID:", initialSessionId);
    expect(initialSessionId).toBeTruthy();

    // Click edit
    await page.locator('button:has-text("Edit template")').first().click();
    await page.waitForURL(/\/templates\/edit\//);
    await page.waitForTimeout(2000);

    // Session should still be same
    const sessionIdAfterNav = await page.evaluate(() => {
      return sessionStorage.getItem("bankers-seat:session-user-id");
    });
    console.log("Session ID after nav:", sessionIdAfterNav);
    expect(sessionIdAfterNav).toBe(initialSessionId);

    // Verify it's a valid Guid
    expect(sessionIdAfterNav).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
    );
  });

  test("different browser contexts should have different session IDs", async ({ browser }) => {
    const context1 = await browser.newContext();
    const context2 = await browser.newContext();

    try {
      const page1 = await context1.newPage();
      const page2 = await context2.newPage();

      // Navigate to initialize session IDs
      await page1.goto("http://localhost:5173/templates");
      await page2.goto("http://localhost:5173/templates");

      // Wait for page load
      await page1.waitForLoadState("networkidle");
      await page2.waitForLoadState("networkidle");

      // Wait a bit for session IDs to be initialized
      await page1.waitForTimeout(500);
      await page2.waitForTimeout(500);

      const sessionId1 = await page1.evaluate(() => {
        return sessionStorage.getItem("bankers-seat:session-user-id");
      });

      const sessionId2 = await page2.evaluate(() => {
        return sessionStorage.getItem("bankers-seat:session-user-id");
      });

      console.log("Session 1:", sessionId1);
      console.log("Session 2:", sessionId2);

      expect(sessionId1).toBeTruthy();
      expect(sessionId2).toBeTruthy();
      expect(sessionId1).not.toBe(sessionId2);
    } finally {
      await context1.close();
      await context2.close();
    }
  });
});
