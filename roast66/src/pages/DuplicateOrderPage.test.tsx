import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import DuplicateOrderPage from "./DuplicateOrderPage";
import { LanguageProvider } from "../i18n/LanguageContext";
import type { OrderDto } from "../types/api";

function renderPage(
  initialState?: { order?: OrderDto; existingOrderId?: number } | null
) {
  return render(
    <LanguageProvider>
      <MemoryRouter initialEntries={[{ pathname: "/duplicate-order", state: initialState }]}>
        <Routes>
          <Route path="/duplicate-order" element={<DuplicateOrderPage />} />
        </Routes>
      </MemoryRouter>
    </LanguageProvider>
  );
}

describe("DuplicateOrderPage", () => {
  it("renders duplicate-order messaging and navigation links", () => {
    renderPage();
    expect(screen.getByRole("heading", { name: /double brew detected/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /check order status/i })).toHaveAttribute(
      "href",
      "/order-status"
    );
    expect(screen.getByRole("link", { name: /place a new order/i })).toHaveAttribute("href", "/order");
  });

  it("shows confirmed order banner when existingOrderId is in state", () => {
    renderPage({ existingOrderId: 42 });
    expect(screen.getByText(/order #42 is confirmed/i)).toBeInTheDocument();
  });

  it("derives existingOrderId from order in state", () => {
    renderPage({ order: { id: 7 } });
    expect(screen.getByText(/order #7 is confirmed/i)).toBeInTheDocument();
  });
});
