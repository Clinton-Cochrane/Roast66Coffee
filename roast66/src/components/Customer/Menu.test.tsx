import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import Menu from "./Menu";
import CategoryType from "../../constants/categories";
import { LanguageProvider } from "../../i18n/LanguageContext";
import type { MenuItemDto } from "../../types/api";

const mockGet = vi.fn();

vi.mock("../../axiosConfig", () => ({
  default: {
    get: (...args: unknown[]) => mockGet(...args),
  },
}));

const mockMenuItem: MenuItemDto = {
  id: 1,
  name: "Espresso",
  description: "Strong coffee",
  price: 3.5,
      categoryType: CategoryType.COFFEE,
      isFeaturedOnHome: false,
};

const specialItem: MenuItemDto = {
  id: 2,
  name: "Blue Flame Nitro",
  description: "A house special",
  price: 5.25,
      categoryType: CategoryType.SPECIALS,
      isFeaturedOnHome: true,
};

const flavorItem: MenuItemDto = {
  id: 3,
  name: "Vanilla Shot",
  description: "Classic vanilla",
  price: 0.5,
      categoryType: CategoryType.FLAVORS,
      isFeaturedOnHome: false,
};

function OrderStateProbe() {
  const location = useLocation();
  const state = location.state as { menuItemId?: number } | null;
  return <div>Selected item: {state?.menuItemId}</div>;
}

function renderMenu() {
  return render(
    <LanguageProvider>
      <MemoryRouter>
        <Routes>
          <Route path="/" element={<Menu />} />
          <Route path="/order" element={<OrderStateProbe />} />
        </Routes>
      </MemoryRouter>
    </LanguageProvider>
  );
}

describe("Menu", () => {
  beforeEach(() => {
    window.history.replaceState(null, "", window.location.pathname);
    mockGet.mockReset();
    mockGet.mockResolvedValue({ data: [mockMenuItem] });
  });

  it("renders loading state initially", () => {
    mockGet.mockImplementation(() => new Promise(() => {}));
    renderMenu();
    expect(screen.getByText("☕")).toBeInTheDocument();
  });

  it("fetches and displays menu items under Drinks", async () => {
    renderMenu();
    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/menu");
    });
    expect((await screen.findAllByText("Drinks")).length).toBeGreaterThan(0);
    expect(screen.getByText("Espresso")).toBeInTheDocument();
    expect(screen.getByText("$3.50")).toBeInTheDocument();
  });

  it("still renders menu heading after fetch failure", async () => {
    mockGet.mockRejectedValue(new Error("Network error"));
    renderMenu();
    expect(await screen.findByText("Our Menu")).toBeInTheDocument();
    await waitFor(() => {
      expect(mockGet).toHaveBeenCalled();
    });
    expect(screen.queryByText("Espresso")).not.toBeInTheDocument();
  });

  it("keeps category navigation sticky and marks the selected category active", async () => {
    mockGet.mockResolvedValue({ data: [mockMenuItem, specialItem, flavorItem] });
    renderMenu();

    const categoryNavigation = await screen.findByRole("navigation", {
      name: "Menu categories",
    });
    const drinksLink = screen.getByRole("link", { name: "Drinks" });
    const specialsLink = screen.getByRole("link", { name: "Specials" });

    expect(categoryNavigation).toContainElement(drinksLink);
    expect(categoryNavigation).toHaveClass("sticky", "mx-0");
    expect(categoryNavigation).not.toHaveClass("md:mx-4", "xl:mx-8");
    expect(categoryNavigation).not.toHaveClass("md:static");
    expect(drinksLink).toHaveAttribute("href", "#drinks");
    expect(specialsLink).toHaveAttribute("href", "#specials");
    expect(specialsLink).toHaveAttribute("aria-current", "location");

    fireEvent.click(drinksLink);

    expect(drinksLink).toHaveAttribute("aria-current", "location");
    expect(drinksLink).toHaveClass("border-[#c77e42]", "bg-[#c77e42]", "text-black");
    expect(specialsLink).not.toHaveAttribute("aria-current");
  });

  it("promotes Specials ahead of Drinks", async () => {
    mockGet.mockResolvedValue({ data: [mockMenuItem, specialItem] });
    renderMenu();

    const specialsHeading = await screen.findByRole("heading", { name: "Specials" });
    const drinksHeading = screen.getByRole("heading", { name: "Drinks" });

    expect(specialsHeading.compareDocumentPosition(drinksHeading)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING
    );
  });

  it("uses consistent category colors for sections and item cards", async () => {
    mockGet.mockResolvedValue({ data: [mockMenuItem, specialItem] });
    renderMenu();

    const specialsHeading = await screen.findByRole("heading", { name: "Specials" });
    const drinksHeading = screen.getByRole("heading", { name: "Drinks" });
    const specialCard = screen.getByRole("heading", { name: specialItem.name }).parentElement;
    const drinkCard = screen.getByRole("heading", { name: mockMenuItem.name }).parentElement;

    expect(specialsHeading).toHaveClass("border-[#c77e42]");
    expect(specialsHeading.closest("section")).toHaveClass("border-[#c77e42]");
    expect(specialCard).toHaveClass("border-[#c77e42]", "bg-[#fffaf4]");

    expect(drinksHeading).toHaveClass("border-[#4a3326]");
    expect(drinksHeading.closest("section")).toHaveClass("border-[#4a3326]", "bg-[#fffaf4]");
    expect(drinkCard).toHaveClass("border-[#4a3326]", "bg-[#eadfd3]");
  });

  it("preserves API order within a category", async () => {
    const latte = { ...mockMenuItem, id: 4, name: "Latte" };
    mockGet.mockResolvedValue({ data: [latte, mockMenuItem] });
    renderMenu();

    const latteHeading = await screen.findByRole("heading", { name: "Latte" });
    const espressoHeading = screen.getByRole("heading", { name: "Espresso" });

    expect(latteHeading.compareDocumentPosition(espressoHeading)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING
    );
  });

  it("opens the order page with the selected directly orderable item", async () => {
    mockGet.mockResolvedValue({ data: [specialItem] });
    renderMenu();

    fireEvent.click(await screen.findByRole("button", { name: "Order Blue Flame Nitro" }));

    expect(screen.getByText("Selected item: 2")).toBeInTheDocument();
  });

  it("shows flavors compactly without a direct order action", async () => {
    mockGet.mockResolvedValue({ data: [flavorItem] });
    renderMenu();

    expect(await screen.findByText("Vanilla Shot")).toBeInTheDocument();
    expect(screen.getByText("Choose flavors as add-ons while building your drink.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Order Vanilla Shot" })).not.toBeInTheDocument();
  });
});
