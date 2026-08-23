import React from "react";
import { describe, it, expect, beforeEach, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import Navigation from "./Navigation";
import { LanguageProvider } from "../i18n/LanguageContext";
import { ORDER_STATUS } from "../constants/orderStatus";
import { ORDER_STATUS_LOOKUP_SESSION_KEY } from "../constants/orderStatusSession";
import { fetchOrderLookup } from "../lib/orderStatusLookup";

vi.mock("../lib/orderStatusLookup", () => ({
  fetchOrderLookup: vi.fn().mockResolvedValue({
    id: 9,
    customerName: "Sam",
    orderStatus: 1,
    orderItems: [],
  }),
}));

describe("Navigation", () => {
  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  it("does not show order tracking dot without session", () => {
    render(
      <LanguageProvider>
        <MemoryRouter>
          <Navigation />
        </MemoryRouter>
      </LanguageProvider>
    );
    expect(screen.queryByRole("link", { name: /return to order status/i })).toBeNull();
    expect(screen.getByText("Local coffee. Timeless roads.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /order now/i })).toHaveAttribute("href", "/order");
    expect(screen.getByRole("link", { name: "Instagram" })).toHaveAttribute(
      "href",
      "https://www.instagram.com/roast66coffee"
    );
  });

  it("switches language from the header", async () => {
    render(
      <LanguageProvider>
        <MemoryRouter>
          <Navigation />
        </MemoryRouter>
      </LanguageProvider>
    );

    fireEvent.click(screen.getByRole("button", { name: /cambiar idioma/i }));
    expect(screen.getByText("Café local. Caminos eternos.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /ordenar ahora/i })).toBeInTheDocument();
  });

  it.each(["Menu", "Merch"])("closes the Shop dropdown after selecting %s", (linkName) => {
    render(
      <LanguageProvider>
        <MemoryRouter>
          <Navigation />
        </MemoryRouter>
      </LanguageProvider>
    );

    const shopMenu = screen.getByText("Shop").closest("details");
    expect(shopMenu).not.toBeNull();

    fireEvent.click(screen.getByText("Shop"));
    expect(shopMenu).toHaveAttribute("open");

    fireEvent.click(screen.getByRole("link", { name: linkName }));
    expect(shopMenu).not.toHaveAttribute("open");
  });

  it("shows a link to order-status when lookup session exists", async () => {
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: "tracking-token-9", orderStatus: 1 })
    );
    render(
      <LanguageProvider>
        <MemoryRouter>
          <Navigation />
        </MemoryRouter>
      </LanguageProvider>
    );
    const tracking = screen.getByRole("link", { name: /return to order status/i });
    expect(tracking).toHaveAttribute("href", "/order-status?token=tracking-token-9");
    await waitFor(() => {
      expect(vi.mocked(fetchOrderLookup)).toHaveBeenCalled();
    });
  });

  it("does not show order tracking dot when session order is completed", () => {
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({
        trackingToken: "tracking-token-9",
        orderStatus: ORDER_STATUS.Completed,
      })
    );
    render(
      <LanguageProvider>
        <MemoryRouter>
          <Navigation />
        </MemoryRouter>
      </LanguageProvider>
    );
    expect(screen.queryByRole("link", { name: /return to order status/i })).toBeNull();
  });
});
