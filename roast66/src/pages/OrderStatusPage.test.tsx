import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { LanguageProvider } from "../i18n/LanguageContext";
import { ORDER_STATUS_LOOKUP_SESSION_KEY } from "../constants/orderStatusSession";

const mockGet = vi.fn();

vi.mock("../axiosConfig", () => ({
  default: {
    get: (...args: unknown[]) => mockGet(...args),
    post: vi.fn(),
  },
}));

vi.mock("react-toastify", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

import OrderStatusPage from "./OrderStatusPage";

const lookupResponse = {
  id: 42,
  customerName: "Alex",
  orderDate: new Date("2025-01-01T12:00:00Z").toISOString(),
  orderStatus: 1,
  orderItems: [{ quantity: 1, menuItem: { name: "Latte" } }],
};

const lookupResponseOther = {
  id: 99,
  customerName: "Bob",
  orderDate: new Date("2025-01-02T12:00:00Z").toISOString(),
  orderStatus: 0,
  orderItems: [{ quantity: 1, menuItem: { name: "Espresso" } }],
};

describe("OrderStatusPage", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.clearAllMocks();
    mockGet.mockResolvedValue({ data: lookupResponse });
  });

  it("runs lookup from session on mount when saved session exists", async () => {
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ orderId: "42", customerName: "Alex", orderStatus: 1 })
    );
    render(
      <LanguageProvider>
        <MemoryRouter>
          <OrderStatusPage />
        </MemoryRouter>
      </LanguageProvider>
    );
    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/order/lookup", {
        params: { orderId: 42, customerName: "Alex" },
      });
    });
    expect(await screen.findByText(/Order #42/i)).toBeInTheDocument();
  });

  it("does not let a slow session restore overwrite a completed manual lookup", async () => {
    let resolveRestore!: (value: { data: typeof lookupResponse }) => void;
    mockGet
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveRestore = resolve;
          })
      )
      .mockImplementationOnce(() => Promise.resolve({ data: lookupResponseOther }));

    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ orderId: "42", customerName: "Alex", orderStatus: 1 })
    );
    const { container } = render(
      <LanguageProvider>
        <MemoryRouter>
          <OrderStatusPage />
        </MemoryRouter>
      </LanguageProvider>
    );

    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledTimes(1);
    });

    const form = container.querySelector("form");
    if (!form) {
      throw new Error("form not found");
    }

    const orderIdInput = screen.getByRole("textbox", { name: /Order ID/i });
    const nameInput = screen.getByRole("textbox", { name: /Your Name/i });

    fireEvent.change(orderIdInput, { target: { value: "99" } });
    fireEvent.change(nameInput, { target: { value: "Bob" } });
    fireEvent.submit(form);

    expect(await screen.findByText(/Order #99/i)).toBeInTheDocument();

    const storedAfterManual = JSON.parse(
      sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY) ?? "{}"
    );
    expect(storedAfterManual.orderId).toBe("99");
    expect(storedAfterManual.customerName).toBe("Bob");

    resolveRestore({ data: lookupResponse });

    await waitFor(() => {
      expect(screen.getByText(/Order #99/i)).toBeInTheDocument();
    });
    expect(screen.queryByText(/Order #42/i)).not.toBeInTheDocument();

    const storedAfterStaleRestore = JSON.parse(
      sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY) ?? "{}"
    );
    expect(storedAfterStaleRestore.orderId).toBe("99");
    expect(storedAfterStaleRestore.customerName).toBe("Bob");
  });
});
