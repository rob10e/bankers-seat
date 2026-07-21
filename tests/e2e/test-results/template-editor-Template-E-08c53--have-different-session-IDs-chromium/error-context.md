# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: template-editor.spec.ts >> Template Editor Flow >> different browser contexts should have different session IDs
- Location: tests\template-editor.spec.ts:102:3

# Error details

```
Error: expect(received).toBeTruthy()

Received: null
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
    - heading "Template catalog" [level=4] [ref=e24]
    - generic [ref=e25]:
      - generic: Search templates
      - generic [ref=e26]:
        - textbox "Search templates" [ref=e27]:
          - /placeholder: Search by name, edition, or tag
        - group:
          - generic: Search templates
    - generic [ref=e28]:
      - generic [ref=e30]:
        - generic [ref=e32]:
          - generic [ref=e33]:
            - heading "Life Journey Banker" [level=5] [ref=e34]
            - generic [ref=e36]: 2-10 players
          - paragraph [ref=e37]: "Edition: Family Edition • Version 1.0.0"
          - paragraph [ref=e38]: An original generic template for life-event and career-journey board games.
          - generic [ref=e39]:
            - generic [ref=e41]: life
            - generic [ref=e43]: career
            - generic [ref=e45]: family
            - generic [ref=e47]: payday
            - generic [ref=e49]: generic
        - generic [ref=e50]:
          - button "Select template" [ref=e51] [cursor=pointer]
          - button "Edit template" [ref=e52] [cursor=pointer]
      - generic [ref=e54]:
        - generic [ref=e56]:
          - generic [ref=e57]:
            - heading "Property Trading Banker" [level=5] [ref=e58]
            - generic [ref=e60]: 2-8 players
          - paragraph [ref=e61]: "Edition: Standard Edition • Version 1.0.0"
          - paragraph [ref=e62]: An original generic template for property-trading board games.
          - generic [ref=e63]:
            - generic [ref=e65]: property
            - generic [ref=e67]: trading
            - generic [ref=e69]: money
            - generic [ref=e71]: generic
        - generic [ref=e72]:
          - button "Select template" [ref=e73] [cursor=pointer]
          - button "Edit template" [ref=e74] [cursor=pointer]
```

# Test source

```ts
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
  94  |     expect(sessionIdAfterNav).toBe(initialSessionId);
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
> 121 |       expect(sessionId1).toBeTruthy();
      |                          ^ Error: expect(received).toBeTruthy()
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