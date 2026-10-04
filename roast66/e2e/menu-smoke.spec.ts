import { expect, test } from "@playwright/test";

test("the built menu renders data from the candidate API", async ({ page }) => {
  const expectedMenuItem = process.env.SMOKE_MENU_ITEM ?? "CI Smoke Latte";
  const menuResponsePromise = page.waitForResponse((response) => {
    const url = new URL(response.url());
    return url.pathname === "/api/menu";
  });

  await page.goto("/menu");

  const menuResponse = await menuResponsePromise;
  expect(menuResponse.ok()).toBe(true);
  const menu = (await menuResponse.json()) as Array<{
    name?: string;
    isArchived?: boolean;
  }>;
  expect(menu).toContainEqual(
    expect.objectContaining({
      name: expectedMenuItem,
      isArchived: false,
    })
  );

  await expect(page.getByRole("heading", { name: "Our Menu" })).toBeVisible();
  await expect(page.getByText(expectedMenuItem, { exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Drinks" })).toBeVisible();

  // Exercise generated CSS, which class-name assertions in jsdom cannot verify.
  const menuHeader = page.locator("header").filter({
    has: page.getByRole("heading", { name: "Our Menu" }),
  });
  await expect(menuHeader).toHaveCSS("padding-left", "32px");
  await expect(menuHeader).toHaveCSS("border-radius", "16px");

  const categoryLink = page.getByRole("link", { name: "Drinks", exact: true });
  await expect(categoryLink).toHaveCSS("display", "inline-flex");
  await expect(categoryLink).toHaveCSS("min-height", "44px");
  await categoryLink.focus();
  await expect(categoryLink).toHaveCSS("outline-style", "solid");
  await expect(categoryLink).toHaveCSS("outline-width", "3px");

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(menuHeader).toHaveCSS("padding-left", "20px");
  await expect(categoryLink).toHaveCSS("min-height", "44px");
});
