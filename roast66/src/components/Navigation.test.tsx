import React from "react";
import { afterEach, describe, it, expect, beforeEach, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import Navigation from "./Navigation";
import { LanguageProvider } from "../i18n/LanguageContext";
import { ORDER_STATUS } from "../constants/orderStatus";
import {
  ORDER_STATUS_LOOKUP_SESSION_KEY,
  ORDER_STATUS_SESSION_UPDATED_EVENT,
} from "../constants/orderStatusSession";
import { fetchOrderLookup } from "../lib/orderStatusLookup";

vi.mock("../lib/orderStatusLookup", async () => {
  const actual = await vi.importActual<typeof import("../lib/orderStatusLookup")>(
    "../lib/orderStatusLookup"
  );
  return {
    ...actual,
    fetchOrderLookup: vi.fn(),
  };
});

const mockedFetchOrderLookup = vi.mocked(fetchOrderLookup);

const lookupResponse = {
  id: 9,
  customerName: "Sam",
  orderStatus: ORDER_STATUS.Preparing,
  orderItems: [],
};

const unavailableError = {
  isAxiosError: true,
  response: {
    status: 404,
    data: { code: "order_status_unavailable" },
  },
};

const serverError = {
  isAxiosError: true,
  response: {
    status: 503,
    data: {},
  },
};

function renderNavigation() {
  return render(
    <LanguageProvider>
      <MemoryRouter>
        <Navigation />
      </MemoryRouter>
    </LanguageProvider>
  );
}

describe("Navigation", () => {
  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
    mockedFetchOrderLookup.mockReset();
    mockedFetchOrderLookup.mockResolvedValue(lookupResponse);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("does not show order tracking dot without session", () => {
    renderNavigation();
    expect(screen.queryByRole("link", { name: /return to order status/i })).toBeNull();
    expect(screen.getByText("Local coffee. Timeless roads.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /order now/i })).toHaveAttribute("href", "/order");
    expect(screen.getByRole("link", { name: "Instagram" })).toHaveAttribute(
      "href",
      "https://www.instagram.com/roast66coffee"
    );
  });

  it("switches language from the header", async () => {
    renderNavigation();

    fireEvent.click(screen.getByRole("button", { name: /cambiar idioma/i }));
    expect(screen.getByText("Café local. Caminos eternos.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /ordenar ahora/i })).toBeInTheDocument();
  });

  it("shows Menu and Merch as direct navigation links without a Shop dropdown", () => {
    renderNavigation();

    expect(screen.queryByText("Shop")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Menu" })).toHaveAttribute("href", "/menu");
    expect(screen.getByRole("link", { name: "Merch" })).toHaveAttribute(
      "href",
      "https://roast-66-coffee.printify.me/products"
    );
  });

  it("shows a link to order-status when lookup session exists", async () => {
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: "tracking-token-9", orderStatus: 1 })
    );
    renderNavigation();
    const tracking = screen.getByRole("link", { name: /return to order status/i });
    expect(tracking).toHaveAttribute("href", "/order-status?token=tracking-token-9");
    await waitFor(() => {
      expect(mockedFetchOrderLookup).toHaveBeenCalled();
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
    renderNavigation();
    expect(screen.queryByRole("link", { name: /return to order status/i })).toBeNull();
  });

  it("clears an unavailable active session and removes its indicator", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockedFetchOrderLookup.mockRejectedValueOnce(unavailableError);
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: "stale-active-token", orderStatus: 1 })
    );
    const sessionUpdated = vi.fn();
    window.addEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, sessionUpdated);

    renderNavigation();

    expect(
      screen.getByRole("link", { name: /return to order status/i })
    ).toBeInTheDocument();
    await waitFor(() => {
      expect(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)).toBeNull();
    });
    expect(
      screen.queryByRole("link", { name: /return to order status/i })
    ).not.toBeInTheDocument();
    expect(sessionUpdated).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(180_000);
    expect(mockedFetchOrderLookup).toHaveBeenCalledTimes(1);
    window.removeEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, sessionUpdated);
  });

  it("clears a completed session when visibility validation finds it unavailable", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.spyOn(document, "visibilityState", "get").mockReturnValue("visible");
    mockedFetchOrderLookup
      .mockResolvedValueOnce({
        ...lookupResponse,
        orderStatus: ORDER_STATUS.Completed,
      })
      .mockRejectedValueOnce(unavailableError);
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({
        trackingToken: "completed-token",
        orderStatus: ORDER_STATUS.Completed,
      })
    );

    renderNavigation();

    await waitFor(() => {
      expect(mockedFetchOrderLookup).toHaveBeenCalledTimes(1);
    });
    await vi.advanceTimersByTimeAsync(180_000);
    expect(mockedFetchOrderLookup).toHaveBeenCalledTimes(1);

    document.dispatchEvent(new Event("visibilitychange"));

    await waitFor(() => {
      expect(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)).toBeNull();
    });
    expect(mockedFetchOrderLookup).toHaveBeenCalledTimes(2);
  });

  it.each([
    ["server", serverError],
    ["network", new Error("offline")],
  ])("preserves an active session after a %s failure", async (_kind, error) => {
    mockedFetchOrderLookup.mockRejectedValueOnce(error);
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: "active-token", orderStatus: 1 })
    );

    renderNavigation();

    await waitFor(() => {
      expect(mockedFetchOrderLookup).toHaveBeenCalledWith("active-token");
    });
    expect(JSON.parse(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)!)).toEqual({
      trackingToken: "active-token",
      orderStatus: 1,
    });
    expect(
      screen.getByRole("link", { name: /return to order status/i })
    ).toBeInTheDocument();
  });

  it("continues updating an active session successfully", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockedFetchOrderLookup
      .mockResolvedValueOnce({
        ...lookupResponse,
        orderStatus: ORDER_STATUS.Received,
      })
      .mockResolvedValue({
        ...lookupResponse,
        orderStatus: ORDER_STATUS.ReadyForPickup,
      });
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: "active-token", orderStatus: ORDER_STATUS.Received })
    );

    renderNavigation();

    await waitFor(() => {
      expect(mockedFetchOrderLookup).toHaveBeenCalledTimes(1);
    });
    await vi.advanceTimersByTimeAsync(90_000);
    await waitFor(() => {
      expect(
        JSON.parse(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)!).orderStatus
      ).toBe(ORDER_STATUS.ReadyForPickup);
    });
    expect(mockedFetchOrderLookup.mock.calls.length).toBeGreaterThanOrEqual(2);
    expect(
      screen.getByRole("link", { name: /return to order status/i })
    ).toBeInTheDocument();
  });
});
