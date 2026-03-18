import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import CategoryType from "../../constants/categories";

jest.mock("../../axiosConfig", () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
  },
}));

import Menu from "./Menu";
import axios from "../../axiosConfig";

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
    render(<Menu />);
    expect(screen.getByText("☕")).toBeInTheDocument();
  });

  it("renders menu items after loading", async () => {
    axios.get.mockResolvedValueOnce({ data: mockMenuItems });
    render(<Menu />);

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
    render(<Menu />);

    await waitFor(() => {
      const headings = screen.getAllByRole("heading", { level: 3 });
      const headingTexts = headings.map((h) => h.textContent);
      expect(headingTexts).toContain("Drinks");
      expect(headingTexts).toContain("Flavors");
    });
  });

  it("fetches menu from /menu endpoint", async () => {
    axios.get.mockResolvedValueOnce({ data: [] });
    render(<Menu />);

    await waitFor(() => {
      expect(axios.get).toHaveBeenCalledWith("/menu");
    });
  });
});
