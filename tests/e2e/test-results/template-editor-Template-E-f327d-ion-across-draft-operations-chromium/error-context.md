# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: template-editor.spec.ts >> Template Editor Flow >> should maintain session across draft operations
- Location: tests\template-editor.spec.ts:79:3

# Error details

```
Error: expect(received).toBe(expected) // Object.is equality

Expected: null
Received: "d8dbd77a-f250-4e94-b9f1-e709d86f86fe"
```

# Page snapshot

```yaml
- generic [ref=e3]:
  - banner [ref=e4]:
    - generic [ref=e5]:
      - generic [ref=e6]:
        - heading "Banker's Seat" [level=6] [ref=e7]
        - generic [ref=e8]: Server-authoritative session companion
      - tablist "Main navigation" [ref=e11]:
        - tab "Home" [ref=e12] [cursor=pointer]
        - tab "Templates" [selected] [ref=e13] [cursor=pointer]
        - tab "Join" [ref=e14] [cursor=pointer]
        - tab "Game" [ref=e15] [cursor=pointer]
        - tab "Settings" [ref=e16] [cursor=pointer]
      - generic [ref=e18]:
        - generic [ref=e20]: Planning UI
        - button "Settings" [ref=e21] [cursor=pointer]
  - generic [ref=e23]:
    - generic [ref=e26]:
      - generic [ref=e27]:
        - heading "generic-life-journey" [level=5] [ref=e28]
        - generic [ref=e29]: Edition family-edition • Version 1.0.0
      - generic [ref=e30]:
        - button "Export" [ref=e31] [cursor=pointer]:
          - img [ref=e33]
          - text: Export
        - button "Save" [disabled]:
          - generic:
            - img
          - text: Save
    - tablist "template editor tabs" [ref=e38]:
      - tab "JSON Editor" [selected] [ref=e39] [cursor=pointer]
      - tab "Live Preview" [ref=e40] [cursor=pointer]
    - tabpanel "JSON Editor" [ref=e43]:
      - generic [ref=e47]:
        - textbox "Enter template JSON..." [ref=e48]: "\"{\\\"schemaVersion\\\":1,\\\"templateId\\\":\\\"generic-life-journey\\\",\\\"templateVersion\\\":\\\"1.0.0\\\",\\\"name\\\":\\\"Life Journey Banker\\\",\\\"description\\\":\\\"An original generic template for life-event and career-journey board games.\\\",\\\"edition\\\":{\\\"id\\\":\\\"family-edition\\\",\\\"name\\\":\\\"Family Edition\\\",\\\"releaseLabel\\\":\\\"Generic sample\\\",\\\"year\\\":2026},\\\"playerCount\\\":{\\\"minimum\\\":2,\\\"maximum\\\":10},\\\"currency\\\":{\\\"code\\\":\\\"LIFE_CREDIT\\\",\\\"symbol\\\":\\\"\\\\u00A4\\\",\\\"name\\\":\\\"credits\\\",\\\"baseUnitName\\\":\\\"credit\\\",\\\"fractionDigits\\\":0,\\\"position\\\":\\\"before\\\"},\\\"denominations\\\":[{\\\"value\\\":1000,\\\"label\\\":\\\"1K\\\"},{\\\"value\\\":5000,\\\"label\\\":\\\"5K\\\"},{\\\"value\\\":10000,\\\"label\\\":\\\"10K\\\"},{\\\"value\\\":50000,\\\"label\\\":\\\"50K\\\"}],\\\"bank\\\":{\\\"startingPlayerBalance\\\":10000,\\\"bankMode\\\":\\\"unlimited\\\",\\\"allowPlayerOverdraft\\\":true},\\\"playerFields\\\":[{\\\"id\\\":\\\"owns-home\\\",\\\"label\\\":\\\"Owns a home\\\",\\\"type\\\":\\\"boolean\\\",\\\"default\\\":false,\\\"visibility\\\":\\\"all\\\",\\\"editableBy\\\":\\\"host-and-owner\\\"},{\\\"id\\\":\\\"children-count\\\",\\\"label\\\":\\\"Children\\\",\\\"type\\\":\\\"counter\\\",\\\"default\\\":0,\\\"minimum\\\":0,\\\"maximum\\\":12,\\\"step\\\":1,\\\"visibility\\\":\\\"all\\\",\\\"editableBy\\\":\\\"host-and-owner\\\"},{\\\"id\\\":\\\"career\\\",\\\"label\\\":\\\"Career\\\",\\\"type\\\":\\\"enum\\\",\\\"default\\\":\\\"none\\\",\\\"options\\\":[{\\\"value\\\":\\\"none\\\",\\\"label\\\":\\\"None\\\"},{\\\"value\\\":\\\"technical\\\",\\\"label\\\":\\\"Technical\\\"},{\\\"value\\\":\\\"creative\\\",\\\"label\\\":\\\"Creative\\\"},{\\\"value\\\":\\\"service\\\",\\\"label\\\":\\\"Service\\\"}],\\\"visibility\\\":\\\"all\\\",\\\"editableBy\\\":\\\"host\\\"},{\\\"id\\\":\\\"salary\\\",\\\"label\\\":\\\"Salary\\\",\\\"type\\\":\\\"currency\\\",\\\"default\\\":10000,\\\"minimum\\\":0,\\\"maximum\\\":1000000,\\\"step\\\":1000,\\\"visibility\\\":\\\"all\\\",\\\"editableBy\\\":\\\"host\\\"}],\\\"actions\\\":[{\\\"id\\\":\\\"payday\\\",\\\"label\\\":\\\"Payday\\\",\\\"description\\\":\\\"Apply a standard payday to the selected player.\\\",\\\"category\\\":\\\"income\\\",\\\"scope\\\":\\\"single-player\\\",\\\"operation\\\":{\\\"type\\\":\\\"bank-to-player\\\",\\\"amount\\\":10000},\\\"confirmation\\\":\\\"never\\\"},{\\\"id\\\":\\\"new-child\\\",\\\"label\\\":\\\"Add child\\\",\\\"category\\\":\\\"life-event\\\",\\\"scope\\\":\\\"single-player\\\",\\\"operation\\\":{\\\"type\\\":\\\"increment-field\\\",\\\"fieldId\\\":\\\"children-count\\\",\\\"amount\\\":1},\\\"confirmation\\\":\\\"never\\\"},{\\\"id\\\":\\\"buy-home\\\",\\\"label\\\":\\\"Buy home\\\",\\\"category\\\":\\\"life-event\\\",\\\"scope\\\":\\\"single-player\\\",\\\"operation\\\":{\\\"type\\\":\\\"composite\\\",\\\"atomic\\\":true,\\\"steps\\\":[{\\\"type\\\":\\\"player-to-bank\\\",\\\"amount\\\":50000},{\\\"type\\\":\\\"set-field\\\",\\\"fieldId\\\":\\\"owns-home\\\",\\\"value\\\":true}]},\\\"confirmation\\\":\\\"always\\\"}],\\\"sessionOptions\\\":[{\\\"id\\\":\\\"starting-balance\\\",\\\"label\\\":\\\"Starting balance\\\",\\\"type\\\":\\\"integer\\\",\\\"default\\\":10000,\\\"minimum\\\":-100000,\\\"maximum\\\":1000000,\\\"mapsTo\\\":\\\"bank.startingPlayerBalance\\\"},{\\\"id\\\":\\\"allow-overdraft\\\",\\\"label\\\":\\\"Allow negative balances\\\",\\\"type\\\":\\\"boolean\\\",\\\"default\\\":true,\\\"mapsTo\\\":\\\"bank.allowPlayerOverdraft\\\"}],\\\"tags\\\":[\\\"life\\\",\\\"career\\\",\\\"family\\\",\\\"payday\\\",\\\"generic\\\"]}\""
        - group
```

# Test source

```ts
  1   | import { test, expect } from "@playwright/test";
  2   | 
  3   | /**
  4   |  * Template editor navigation and draft creation flow tests.
  5   |  *
  6   |  * These tests verify:
  7   |  * 1. Navigation to template catalog
  8   |  * 2. Clicking "Edit template" button
  9   |  * 3. Draft creation with persistent session ID
  10  |  * 4. Editor loading with template data
  11  |  * 5. JSON editing and saving
  12  |  * 6. Cross-session isolation
  13  |  */
  14  | 
  15  | test.describe("Template Editor Flow", () => {
  16  |   test.beforeEach(async ({ page }) => {
  17  |     // Navigate to catalog
  18  |     await page.goto("http://localhost:5173/templates");
  19  |     await page.waitForLoadState("networkidle");
  20  |   });
  21  | 
  22  |   test("should display template catalog with edit buttons", async ({ page }) => {
  23  |     // Check that templates are loaded
  24  |     await expect(page.locator("text=Life Journey Banker")).toBeVisible();
  25  |     await expect(page.locator("text=Property Trading Banker")).toBeVisible();
  26  | 
  27  |     // Check that edit buttons exist
  28  |     const editButtons = page.locator('button:has-text("Edit template")');
  29  |     await expect(editButtons.first()).toBeVisible();
  30  |   });
  31  | 
  32  |   test("should navigate to editor and load draft without spinner", async ({ page, context }) => {
  33  |     // Get session storage before test
  34  |     const sessionId = await page.evaluate(() => {
  35  |       return sessionStorage.getItem("bankers-seat:session-user-id");
  36  |     });
  37  |     console.log("Session ID before:", sessionId);
  38  | 
  39  |     // Click first edit button
  40  |     await page.locator('button:has-text("Edit template")').first().click();
  41  | 
  42  |     // Wait for navigation
  43  |     await page.waitForURL(/\/templates\/edit\//);
  44  |     expect(page.url()).toMatch(/\/templates\/edit\//);
  45  | 
  46  |     // Wait for editor to load (spinner should disappear)
  47  |     await page.waitForTimeout(2000); // Give component time to fetch
  48  |     
  49  |     // Check that we see editor elements, not just spinner
  50  |     await expect(page.locator('textarea, [contenteditable="true"], input[type="text"]')).toBeVisible();
  51  | 
  52  |     // Should NOT see loading spinner
  53  |     const spinner = page.locator("svg.MuiCircularProgress-svg, [role='progressbar']");
  54  |     await expect(spinner).not.toBeVisible({ timeout: 5000 });
  55  | 
  56  |     // Check session ID persists
  57  |     const sessionIdAfter = await page.evaluate(() => {
  58  |       return sessionStorage.getItem("bankers-seat:session-user-id");
  59  |     });
  60  |     console.log("Session ID after:", sessionIdAfter);
  61  |     expect(sessionIdAfter).toBeTruthy();
  62  |   });
  63  | 
  64  |   test("should display template JSON in editor", async ({ page }) => {
  65  |     // Click edit button and wait for nav
  66  |     await page.locator('button:has-text("Edit template")').first().click();
  67  |     await page.waitForURL(/\/templates\/edit\//);
  68  |     await page.waitForTimeout(2000);
  69  | 
  70  |     // Check for JSON content
  71  |     const jsonArea = page.locator("textarea, [role='textbox']").first();
  72  |     const content = await jsonArea.inputValue();
  73  | 
  74  |     // Should contain JSON structure
  75  |     expect(content).toContain('"');
  76  |     expect(content).toContain(":");
  77  |   });
  78  | 
  79  |   test("should maintain session across draft operations", async ({ page }) => {
  80  |     // Get initial session ID
  81  |     const initialSessionId = await page.evaluate(() => {
  82  |       return sessionStorage.getItem("bankers-seat:session-user-id");
  83  |     });
  84  | 
  85  |     // Click edit
  86  |     await page.locator('button:has-text("Edit template")').first().click();
  87  |     await page.waitForURL(/\/templates\/edit\//);
  88  |     await page.waitForTimeout(2000);
  89  | 
  90  |     // Session should still be same
  91  |     const sessionIdAfterNav = await page.evaluate(() => {
  92  |       return sessionStorage.getItem("bankers-seat:session-user-id");
  93  |     });
> 94  |     expect(sessionIdAfterNav).toBe(initialSessionId);
      |                               ^ Error: expect(received).toBe(expected) // Object.is equality
  95  | 
  96  |     // Verify it's a valid Guid
  97  |     expect(sessionIdAfterNav).toMatch(
  98  |       /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
  99  |     );
  100 |   });
  101 | 
  102 |   test("different browser contexts should have different session IDs", async ({ browser }) => {
  103 |     const context1 = await browser.newContext();
  104 |     const context2 = await browser.newContext();
  105 | 
  106 |     try {
  107 |       const page1 = await context1.newPage();
  108 |       const page2 = await context2.newPage();
  109 | 
  110 |       await page1.goto("http://localhost:5173/templates");
  111 |       await page2.goto("http://localhost:5173/templates");
  112 | 
  113 |       const sessionId1 = await page1.evaluate(() => {
  114 |         return sessionStorage.getItem("bankers-seat:session-user-id");
  115 |       });
  116 | 
  117 |       const sessionId2 = await page2.evaluate(() => {
  118 |         return sessionStorage.getItem("bankers-seat:session-user-id");
  119 |       });
  120 | 
  121 |       expect(sessionId1).toBeTruthy();
  122 |       expect(sessionId2).toBeTruthy();
  123 |       expect(sessionId1).not.toBe(sessionId2);
  124 |     } finally {
  125 |       await context1.close();
  126 |       await context2.close();
  127 |     }
  128 |   });
  129 | });
  130 | 
```