import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
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
};

function renderMenu() {
  return render(
    <LanguageProvider>
      <MemoryRouter>
        <Menu />
      </MemoryRouter>
    </LanguageProvider>
  );
}

describe("Menu", () => {
  beforeEach(() => {
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
    expect(await screen.findByText("Drinks")).toBeInTheDocument();
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
});
