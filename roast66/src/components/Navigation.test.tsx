import React from "react";
import { describe, it, expect, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import Navigation from "./Navigation";
import { LanguageProvider } from "../i18n/LanguageContext";
import { ORDER_STATUS_LOOKUP_SESSION_KEY } from "../constants/orderStatusSession";

describe("Navigation", () => {
  beforeEach(() => {
    sessionStorage.clear();
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
  });

  it("shows a link to order-status when lookup session exists", () => {
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ orderId: "9", customerName: "Sam", orderStatus: 1 })
    );
    render(
      <LanguageProvider>
        <MemoryRouter>
          <Navigation />
        </MemoryRouter>
      </LanguageProvider>
    );
    const tracking = screen.getByRole("link", { name: /return to order status/i });
    expect(tracking).toHaveAttribute("href", "/order-status");
  });
});
