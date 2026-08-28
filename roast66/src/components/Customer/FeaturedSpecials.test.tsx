import React from "react";
import { act, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import FeaturedSpecials from "./FeaturedSpecials";
import { LanguageProvider } from "../../i18n/LanguageContext";

const mockGet = vi.fn();

vi.mock("../../axiosConfig", () => ({
  default: { get: (...args: unknown[]) => mockGet(...args) },
}));

describe("FeaturedSpecials", () => {
  beforeEach(() => {
    mockGet.mockReset();
  });

  it("shows useful fallback specials while the menu request is pending", async () => {
    mockGet.mockResolvedValue({ data: [] });

    render(
      <LanguageProvider>
        <MemoryRouter>
          <FeaturedSpecials />
        </MemoryRouter>
      </LanguageProvider>
    );

    expect(screen.getByText("Coffee")).toBeInTheDocument();
    expect(screen.getByText("Espresso Shot")).toBeInTheDocument();
    expect(screen.getByText("Latte")).toBeInTheDocument();
    expect(await screen.findByText(/specials are being prepared/i)).toBeInTheDocument();
  });

  it("shows the first three items from the existing specials category", async () => {
    mockGet.mockResolvedValue({
      data: [
        { id: 1, name: "Regular coffee", description: "Classic", price: 3, categoryType: 0, isFeaturedOnHome: false },
        { id: 2, name: "Mrs. Brownie Latte", description: "Coconut and caramel", price: 7.23, categoryType: 1, isFeaturedOnHome: true },
        { id: 3, name: "Shitbox LUV Fuel", description: "Triple espresso", price: 5, categoryType: 1, isFeaturedOnHome: true },
        { id: 4, name: "Black SS Lemonade", description: "Raspberry and pomegranate", price: 2.5, categoryType: 1, isFeaturedOnHome: true },
        { id: 5, name: "Fourth special", description: "Not shown", price: 4, categoryType: 1, isFeaturedOnHome: true },
      ],
    });

    render(
      <LanguageProvider>
        <MemoryRouter>
          <FeaturedSpecials />
        </MemoryRouter>
      </LanguageProvider>
    );

    expect(await screen.findByText("Mrs. Brownie Latte")).toBeInTheDocument();
    expect(screen.getByText("Shitbox LUV Fuel")).toBeInTheDocument();
    expect(screen.getByText("Black SS Lemonade")).toBeInTheDocument();
    expect(screen.queryByText("Coffee")).not.toBeInTheDocument();
    expect(screen.queryByText("Espresso Shot")).not.toBeInTheDocument();
    expect(screen.queryByText("Latte")).not.toBeInTheDocument();
    expect(screen.queryByText("Regular coffee")).not.toBeInTheDocument();
    expect(screen.queryByText("Fourth special")).not.toBeInTheDocument();
    expect(mockGet).toHaveBeenCalledWith("/menu");
  });

  it("keeps the fallback specials when the menu request fails", async () => {
    let rejectRequest: (reason?: unknown) => void = () => {};
    mockGet.mockImplementation(
      () =>
        new Promise((_, reject) => {
          rejectRequest = reject;
        })
    );

    render(
      <LanguageProvider>
        <MemoryRouter>
          <FeaturedSpecials />
        </MemoryRouter>
      </LanguageProvider>
    );

    await act(async () => {
      rejectRequest(new Error("Menu unavailable"));
    });

    expect(screen.getByText("Coffee")).toBeInTheDocument();
    expect(screen.getByText("Espresso Shot")).toBeInTheDocument();
    expect(screen.getByText("Latte")).toBeInTheDocument();
  });
});
