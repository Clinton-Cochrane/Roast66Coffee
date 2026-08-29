import React from "react";
import { afterEach, describe, it, expect, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
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

function unavailableLookupError(): unknown {
  return {
    isAxiosError: true,
    response: {
      status: 404,
      data: {
        code: "order_status_unavailable",
        message: "This order status is no longer available.",
      },
    },
  };
}

function transientLookupError(): unknown {
  return {
    isAxiosError: true,
    response: {
      status: 503,
      data: { message: "Temporarily unavailable" },
    },
  };
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{`${location.pathname}${location.search}`}</div>;
}

function renderPage(initialEntry = "/order-status") {
  return render(
    <LanguageProvider>
      <MemoryRouter initialEntries={[initialEntry]}>
        <OrderStatusPage />
        <LocationProbe />
      </MemoryRouter>
    </LanguageProvider>
  );
}

describe("OrderStatusPage", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.clearAllMocks();
    mockGet.mockResolvedValue({ data: lookupResponse });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("runs lookup from session on mount when saved session exists", async () => {
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: "token-42", orderStatus: 1 })
    );
    renderPage();
    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/order/track/token-42");
    });
    expect(await screen.findByText(/Order #42/i)).toBeInTheDocument();
  });

  it("loads a valid bookmarked tracking link normally", async () => {
    renderPage("/order-status?token=bookmark-token");

    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/order/track/bookmark-token");
    });
    expect(await screen.findByText(/Order #42/i)).toBeInTheDocument();
    expect(JSON.parse(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)!)).toEqual({
      trackingToken: "bookmark-token",
      orderStatus: 1,
    });
  });

  it("shows a neutral message and clears an unavailable bookmarked session", async () => {
    mockGet.mockRejectedValueOnce(unavailableLookupError());
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: "expired-token", orderStatus: 3 })
    );

    renderPage("/order-status?token=expired-token");

    expect(await screen.findByRole("status")).toHaveTextContent(
      "This order status is no longer available."
    );
    expect(screen.getByRole("textbox", { name: /tracking code/i })).toHaveValue("");
    expect(screen.queryByText("Alex")).not.toBeInTheDocument();
    expect(screen.queryByText("Latte")).not.toBeInTheDocument();
    expect(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)).toBeNull();
    await waitFor(() => {
      expect(screen.getByTestId("location")).toHaveTextContent("/order-status");
    });
    expect(screen.getByTestId("location")).not.toHaveTextContent("token=");
    expect(mockGet).toHaveBeenCalledTimes(1);
  });

  it("shows the same unavailable state after a manual lookup", async () => {
    mockGet.mockRejectedValueOnce(unavailableLookupError());
    const { container } = renderPage();
    const form = container.querySelector("form");
    if (!form) {
      throw new Error("form not found");
    }

    fireEvent.change(screen.getByRole("textbox", { name: /tracking code/i }), {
      target: { value: "unknown-token" },
    });
    fireEvent.submit(form);

    expect(await screen.findByRole("status")).toHaveTextContent(
      "This order status is no longer available."
    );
    expect(screen.getByRole("textbox", { name: /tracking code/i })).toHaveValue("");
  });

  it("preserves saved state and the bookmark after a transient restore failure", async () => {
    mockGet.mockRejectedValueOnce(transientLookupError());
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: "saved-token", orderStatus: 1 })
    );

    renderPage("/order-status?token=saved-token");

    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/order/track/saved-token");
    });
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: /tracking code/i })).toHaveValue(
      "saved-token"
    );
    expect(JSON.parse(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)!)).toEqual({
      trackingToken: "saved-token",
      orderStatus: 1,
    });
    expect(screen.getByTestId("location")).toHaveTextContent(
      "/order-status?token=saved-token"
    );
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
    const { container } = renderPage();

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

    const { container } = renderPage();

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
  });

  it("stops polling after a tracked order becomes unavailable", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockGet
      .mockResolvedValueOnce({ data: lookupResponse })
      .mockRejectedValueOnce(unavailableLookupError());
    const { container } = renderPage();
    const form = container.querySelector("form");
    if (!form) {
      throw new Error("form not found");
    }

    fireEvent.change(screen.getByRole("textbox", { name: /tracking code/i }), {
      target: { value: "token-42" },
    });
    fireEvent.submit(form);
    expect(await screen.findByText(/Order #42/i)).toBeInTheDocument();

    await vi.advanceTimersByTimeAsync(45_000);

    expect(await screen.findByRole("status")).toHaveTextContent(
      "This order status is no longer available."
    );
    expect(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)).toBeNull();
    expect(mockGet).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(90_000);
    expect(mockGet).toHaveBeenCalledTimes(2);
  });
});
