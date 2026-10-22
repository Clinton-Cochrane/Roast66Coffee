import { expect, test } from "@playwright/test";

test("public pages remain readable with JavaScript disabled", async ({ browser, baseURL }) => {
  const context = await browser.newContext({ javaScriptEnabled: false, baseURL });
  const page = await context.newPage();
  try {
    for (const path of ["/", "/about", "/menu", "/order"]) {
      const response = await page.goto(path);
      expect(response?.status()).toBe(200);
      await expect(page.locator("h1")).toBeVisible();
      await expect(page.locator('link[rel="canonical"]')).toHaveAttribute("href", `https://www.roast66coffee.com${path}`);
      await expect(page.locator('meta[name="robots"]')).toHaveAttribute("content", "index, follow");
    }
    await page.goto("/");
    await page.locator('main a[href="/menu#specials"]').click();
    await expect(page.getByRole("heading", { level: 1, name: "Our Menu" })).toBeVisible();
  } finally {
    await context.close();
  }
});

test("client navigation updates the prerendered metadata without duplicates", async ({ page }) => {
  await page.goto("/");
  await page.locator('footer a[href="/about"]').click();
  await expect(page).toHaveURL(/\/about$/);
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute("href", "https://www.roast66coffee.com/about");
  await expect(page.locator('script[type="application/ld+json"]')).toHaveCount(0);
  await page.locator('footer a[href="/order"]').click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expect(page.locator('link[rel="canonical"]')).toHaveCount(1);
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute("href", "https://www.roast66coffee.com/order");
});

test("direct app routes are noindex and missing crawl files are 404", async ({ request }) => {
  for (const path of ["/admin", "/admin-login", "/cash", "/order-status", "/order/confirmation", "/order/duplicate"]) {
    const response = await request.get(path);
    expect(response.status()).toBe(200);
    const html = await response.text();
    expect(html).toContain('name="robots" content="noindex, nofollow"');
    expect(html).not.toContain('rel="canonical"');
  }
  const unknown = await request.get("/unknown-page");
  // Nginx can serve the noindex app fallback; an unconfigured CDN returns 404.
  if (unknown.status() !== 404) {
    expect(unknown.status()).toBe(200);
    expect(await unknown.text()).toContain('name="robots" content="noindex, nofollow"');
  }
  for (const path of ["/missing.js", "/missing.xml"]) {
    expect((await request.get(path)).status()).toBe(404);
  }
  const sitemap = await request.get("/sitemap.xml");
  expect(sitemap.headers()["content-type"]).toContain("xml");
  expect(await sitemap.text()).toContain("<urlset");
});
