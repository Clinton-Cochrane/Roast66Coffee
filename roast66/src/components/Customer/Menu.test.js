import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import CategoryType from "../../constants/categories";
import { LanguageProvider } from "../../i18n/LanguageContext";

jest.mock("../../axiosConfig", () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
  },
}));

const mockNavigate = jest.fn();

jest.mock("react-router-dom", () => ({
  __esModule: true,
  ...jest.requireActual("react-router-dom"),
  useNavigate: () => mockNavigate,
}));

import Menu from "./Menu";
import axios from "../../axiosConfig";

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
  const mockMenuItems = [
    {
      id: 1,
      name: "Espresso",
      price: 2.5,
      description: "Strong coffee",
      categoryType: CategoryType.COFFEE,
    },
    {
      id: 2,
      name: "Vanilla",
      price: 0.5,
      description: "Sweet flavor",
      categoryType: CategoryType.FLAVORS,
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("shows loading state initially", () => {
    axios.get.mockImplementation(() => new Promise(() => {}));
    renderMenu();
    expect(screen.getByText("☕")).toBeInTheDocument();
  });

  it("renders menu items after loading", async () => {
    axios.get.mockResolvedValueOnce({ data: mockMenuItems });
    renderMenu();

    await waitFor(() => {
      expect(screen.getByText("Our Menu")).toBeInTheDocument();
    });
    await waitFor(() => {
      expect(screen.getByText("Espresso")).toBeInTheDocument();
      expect(screen.getByText("Vanilla")).toBeInTheDocument();
    });
  });

  it("groups items by category", async () => {
    axios.get.mockResolvedValueOnce({ data: mockMenuItems });
    renderMenu();

    await waitFor(() => {
      const headings = screen.getAllByRole("heading", { level: 3 });
      const headingTexts = headings.map((h) => h.textContent);
      expect(headingTexts).toContain("Drinks");
      expect(headingTexts).toContain("Flavors");
    });
  });

  it("fetches menu from /menu endpoint", async () => {
    axios.get.mockResolvedValueOnce({ data: [] });
    renderMenu();

    await waitFor(() => {
      expect(axios.get).toHaveBeenCalledWith("/menu");
    });
  });

  it("navigates to order with menuItemId for orderable items only", async () => {
    axios.get.mockResolvedValueOnce({ data: mockMenuItems });
    renderMenu();

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /order this item/i })
      ).toBeInTheDocument();
    });

    expect(
      screen.getAllByRole("button", { name: /order this item/i })
    ).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: /order this item/i }));

    expect(mockNavigate).toHaveBeenCalledWith("/order", {
      state: { menuItemId: 1 },
    });
  });
});
