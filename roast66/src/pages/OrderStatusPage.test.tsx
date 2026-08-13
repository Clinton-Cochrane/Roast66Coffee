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
      JSON.stringify({ trackingToken: "token-42", orderStatus: 1 })
    );
    render(
      <LanguageProvider>
        <MemoryRouter>
          <OrderStatusPage />
        </MemoryRouter>
      </LanguageProvider>
    );
    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/order/track/token-42");
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
      JSON.stringify({ trackingToken: "token-42", orderStatus: 1 })
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

    const trackingInput = screen.getByRole("textbox", { name: /tracking code/i });

    fireEvent.change(trackingInput, { target: { value: "token-99" } });
    fireEvent.submit(form);

    expect(await screen.findByText(/Order #99/i)).toBeInTheDocument();

    const storedAfterManual = JSON.parse(
      sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY) ?? "{}"
    );
    expect(storedAfterManual.trackingToken).toBe("token-99");

    resolveRestore({ data: lookupResponse });

    await waitFor(() => {
      expect(screen.getByText(/Order #99/i)).toBeInTheDocument();
    });
    expect(screen.queryByText(/Order #42/i)).not.toBeInTheDocument();

    const storedAfterStaleRestore = JSON.parse(
      sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY) ?? "{}"
    );
    expect(storedAfterStaleRestore.trackingToken).toBe("token-99");
  });

  it("polling uses last successful lookup credentials, not in-progress form edits", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });

    const { container } = render(
      <LanguageProvider>
        <MemoryRouter>
          <OrderStatusPage />
        </MemoryRouter>
      </LanguageProvider>
    );

    const form = container.querySelector("form");
    if (!form) {
      throw new Error("form not found");
    }

    const trackingInput = screen.getByRole("textbox", { name: /tracking code/i });

    fireEvent.change(trackingInput, { target: { value: "token-42" } });
    fireEvent.submit(form);

    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/order/track/token-42");
    });

    mockGet.mockClear();

    fireEvent.change(trackingInput, { target: { value: "token-being-typed" } });

    await vi.advanceTimersByTimeAsync(45_000);

    expect(mockGet).toHaveBeenCalledTimes(1);
    expect(mockGet).toHaveBeenCalledWith("/order/track/token-42");

    vi.useRealTimers();
  });
});
