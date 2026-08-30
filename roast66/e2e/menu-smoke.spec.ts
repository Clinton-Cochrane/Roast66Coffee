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
});
