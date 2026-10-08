import React from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import CashGate from "../components/Admin/CashGate";
import OrderPage from "./OrderPage";
import OrderConfirmationPage from "./OrderConfirmationPage";
import DuplicateOrderPage from "./DuplicateOrderPage";
import { LanguageProvider } from "../i18n/LanguageContext";
import CategoryType from "../constants/categories";
import type { OrderDto } from "../types/api";

const http = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
const toasts = vi.hoisted(() => ({ error: vi.fn(), warning: vi.fn(), success: vi.fn() }));

vi.mock("../axiosConfig", () => ({ default: http }));
vi.mock("react-toastify", () => ({ toast: toasts }));
vi.mock("../components/Admin/StaffDevicePrompt", () => ({ default: () => null }));

const menu = [
  { id: 1, name: "Espresso", price: 2.5, description: "Coffee", categoryType: CategoryType.COFFEE },
  { id: 2, name: "Vanilla", price: 0.5, description: "Flavor", categoryType: CategoryType.FLAVORS },
];
const order: OrderDto = {
  id: 42,
  customerName: "Ada",
  orderStatus: 0,
  trackingToken: "private-tracking-token",
  orderItems: [{ quantity: 1, menuItem: { name: "Espresso", price: 2.5 } }],
};

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="current-path">{location.pathname}</output>;
}

function renderFlow(pathname = "/cash", state?: Record<string, unknown>) {
  return render(
    <LanguageProvider>
      <MemoryRouter initialEntries={[{ pathname, state }]}>
        <LocationProbe />
        <Routes>
          <Route path="/cash" element={<CashGate />} />
          <Route path="/order" element={<OrderPage />} />
          <Route path="/order/confirmation" element={<OrderConfirmationPage />} />
          <Route path="/order/duplicate" element={<DuplicateOrderPage />} />
        </Routes>
      </MemoryRouter>
    </LanguageProvider>
  );
}

async function buildOrder() {
  expect(screen.getByRole("heading", { name: "Place Order" })).toBeInTheDocument();
  expect(screen.getByText("Build homemade drinks for the road in just a few taps.")).toBeInTheDocument();
  fireEvent.change(screen.getByRole("textbox", { name: "Name" }), { target: { value: "Ada" } });
  fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
  fireEvent.click(await screen.findByRole("button", { name: "Order Espresso" }));
}

function submitOrder() {
  fireEvent.click(screen.getByRole("button", { name: "Place Order" }));
}

function orderListRequests() {
  return http.get.mock.calls.filter(([url]) => url === "/admin/orders");
}

describe("cashier order entry and public route regression", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    localStorage.clear();
    sessionStorage.clear();
    localStorage.setItem("roast66_locale", "en");
    localStorage.setItem("token", "x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
    vi.stubGlobal("matchMedia", vi.fn(() => ({
      matches: false,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })));
    http.get.mockImplementation(async (url: string) => {
      if (url === "/menu") return { data: menu };
      if (url === "/admin/orders/new-count") return { data: { count: 0 } };
      if (url === "/admin/orders") return { data: {
        items: [], page: 1, pageSize: 50, totalItems: 0, totalPages: 0,
        hasPreviousPage: false, hasNextPage: false,
      } };
      throw new Error(`Unexpected GET ${url}`);
    });
    http.post.mockResolvedValue({ data: order, status: 201 });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it.each(["Cash", "Other"])("records %s from the cashier Orders panel and preserves the new-order draft", async (label) => {
    let currentOrder = { ...order, total: 2.5, paidUtc: null as string | null, paymentProvider: null as string | null };
    http.get.mockImplementation(async (url: string) => {
      if (url === "/menu") return { data: menu };
      if (url === "/admin/orders/new-count") return { data: { count: 0 } };
      if (url === "/admin/orders") return { data: {
        items: [currentOrder], page: 1, pageSize: 50, totalItems: 1, totalPages: 1,
        hasPreviousPage: false, hasNextPage: false,
      } };
      throw new Error(`Unexpected GET ${url}`);
    });
    http.post.mockImplementation(async (_url: string, body: { method: string }) => {
      currentOrder = { ...currentOrder, paidUtc: "2026-08-26T10:05:00Z", paymentProvider: body.method };
      return { data: {
        paymentId: "manual-receipt", orderId: 42, method: body.method, amount: 2.5,
        currency: "USD", paidUtc: currentOrder.paidUtc, wasReplay: false,
      } };
    });
    renderFlow();
    await buildOrder();
    fireEvent.click(screen.getByRole("tab", { name: "Orders" }));
    fireEvent.click(await screen.findByRole("button", { name: "Take Payment" }));
    fireEvent.click(screen.getByRole("button", { name: label }));
    expect(screen.getByText(`Have you received $2.50 by ${label} for Order #42?`)).toBeInTheDocument();
    expect(http.post).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Confirm payment received" }));
    expect(await screen.findByText(`Paid · ${label}`)).toBeInTheDocument();
    expect(http.post).toHaveBeenCalledExactlyOnceWith("/payments/manual", { orderId: 42, method: label.toLowerCase() });
    expect(screen.getByTestId("current-path")).toHaveTextContent("/cash");
    fireEvent.click(screen.getByRole("tab", { name: "New Order" }));
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Ada");
    expect(screen.getByRole("spinbutton", { name: "Quantity for Espresso" })).toHaveValue(1);
  });

  it("preserves draft details, customizations, filters, and the mounted Orders view", async () => {
    renderFlow();
    await buildOrder();
    fireEvent.change(screen.getByRole("textbox", { name: "Email for order updates (optional)" }), {
      target: { value: "ada@example.com" },
    });
    fireEvent.change(screen.getByRole("spinbutton", { name: "Quantity for Espresso" }), {
      target: { value: "2" },
    });
    fireEvent.change(screen.getByRole("textbox", { name: "Notes (optional)" }), {
      target: { value: "Light ice" },
    });
    fireEvent.change(screen.getByRole("combobox", { name: "Add a Flavor" }), {
      target: { value: JSON.stringify(menu[1]) },
    });
    fireEvent.click(screen.getByRole("tab", { name: "Orders" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Apply filters" })).toBeEnabled());
    fireEvent.change(screen.getByRole("searchbox", { name: "Search orders" }), {
      target: { value: "Ada" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Apply filters" }));
    await waitFor(() => expect(orderListRequests()).toHaveLength(2));
    await waitFor(() => expect(screen.getByRole("button", { name: "Apply filters" })).toBeEnabled());

    fireEvent.click(screen.getByRole("tab", { name: "New Order" }));
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Ada");
    expect(screen.getByRole("textbox", { name: "Email for order updates (optional)" })).toHaveValue("ada@example.com");
    expect(screen.getByRole("spinbutton", { name: "Quantity for Espresso" })).toHaveValue(2);
    expect(screen.getByRole("textbox", { name: "Notes (optional)" })).toHaveValue("Light ice");
    expect(screen.getByRole("spinbutton", { name: "Quantity for Vanilla" })).toHaveValue(1);
    expect(screen.getAllByText("$5.50").length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole("tab", { name: "Orders" }));
    expect(screen.getByRole("searchbox", { name: "Search orders" })).toHaveValue("Ada");
    expect(orderListRequests()).toHaveLength(2);
    expect(screen.getByTestId("current-path")).toHaveTextContent("/cash");

    fireEvent.click(screen.getByRole("tab", { name: "New Order" }));
    const countRequests = http.get.mock.calls.filter(([url]) => url === "/admin/orders/new-count").length;
    fireEvent(window, new Event("focus"));
    await waitFor(() => expect(
      http.get.mock.calls.filter(([url]) => url === "/admin/orders/new-count")
    ).toHaveLength(countRequests + 1));
  });

  it("submits through the existing API, keeps confirmation, and starts a clean intentional order", async () => {
    renderFlow();
    await buildOrder();
    fireEvent.change(screen.getByRole("textbox", { name: "Email for order updates (optional)" }), {
      target: { value: "ada@example.com" },
    });
    fireEvent.change(screen.getByRole("searchbox", { name: "Search drinks" }), {
      target: { value: "Espresso" },
    });
    fireEvent.change(screen.getByRole("textbox", { name: "Notes (optional)" }), {
      target: { value: "Light ice" },
    });
    submitOrder();
    const confirmation = await screen.findByRole("heading", { name: "Order confirmed" });
    expect(confirmation).toHaveFocus();
    expect(screen.getByText("Order #42")).toBeInTheDocument();
    expect(screen.getByText("Ada")).toBeInTheDocument();
    expect(screen.getByTestId("current-path")).toHaveTextContent("/cash");
    expect(http.post).toHaveBeenCalledWith("/order", {
      customerName: "Ada", customerPhone: null, customerEmail: "ada@example.com", customerNotificationOptIn: true,
      orderItems: [{ menuItemId: 1, quantity: 1, notes: "Light ice", addOns: [] }],
    }, { headers: { "X-Idempotency-Key": expect.any(String) } });
    const firstKey = http.post.mock.calls[0][2].headers["X-Idempotency-Key"];

    fireEvent.click(screen.getByRole("button", { name: "Orders" }));
    expect(screen.getByRole("tabpanel", { name: "Orders" })).toBeVisible();
    expect(screen.getByRole("tab", { name: "Orders" })).toHaveFocus();
    fireEvent.click(screen.getByRole("tab", { name: "New Order" }));
    expect(screen.getByRole("heading", { name: "Order confirmed" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "New Order" })).toHaveFocus();
    fireEvent.click(screen.getByRole("button", { name: "New Order" }));
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("");
    expect(screen.getByRole("textbox", { name: "Email for order updates (optional)" })).toHaveValue("");
    expect(screen.getByRole("searchbox", { name: "Search drinks" })).toHaveValue("");
    expect(screen.getByRole("button", { name: "Daily Specials" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.queryByTestId("order-item")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Place Order" })).toBeDisabled();

    await buildOrder();
    fireEvent.change(screen.getByRole("textbox", { name: "Email for order updates (optional)" }), {
      target: { value: "ada@example.com" },
    });
    fireEvent.change(screen.getByRole("textbox", { name: "Notes (optional)" }), {
      target: { value: "Light ice" },
    });
    submitOrder();
    await screen.findByRole("heading", { name: "Order confirmed" });
    expect(http.post.mock.calls[1][2].headers["X-Idempotency-Key"]).not.toBe(firstKey);
  });

  it("preserves Orders pagination and reloads the list only through its existing Refresh action", async () => {
    const defaultGet = http.get.getMockImplementation()!;
    http.get.mockImplementation(async (url: string, config?: { params?: { page?: number } }) => {
      if (url !== "/admin/orders") return defaultGet(url, config);
      const page = config?.params?.page ?? 1;
      return { data: {
        items: [order], page, pageSize: 50, totalItems: 51, totalPages: 2,
        hasPreviousPage: page > 1, hasNextPage: page < 2,
      } };
    });
    renderFlow();
    fireEvent.click(screen.getByRole("tab", { name: "Orders" }));
    await screen.findByText("Page 1 of 2 · 51 orders");
    await waitFor(() => expect(screen.getByRole("button", { name: "Next" })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await screen.findByText("Page 2 of 2 · 51 orders");
    await waitFor(() => expect(screen.getByRole("button", { name: "Refresh" })).toBeEnabled());
    fireEvent.click(screen.getByRole("tab", { name: "New Order" }));
    fireEvent.click(screen.getByRole("tab", { name: "Orders" }));
    expect(screen.getByText("Page 2 of 2 · 51 orders")).toBeVisible();
    expect(orderListRequests()).toHaveLength(2);
    fireEvent.click(screen.getByRole("button", { name: "Refresh" }));
    await waitFor(() => expect(orderListRequests()).toHaveLength(3));
    expect(orderListRequests()[2][1]).toEqual({ params: { page: 2, status: "all" } });
  });

  it("View Order selects Orders, focuses the exact card, and clearing restores default filters and page", async () => {
    const defaultGet = http.get.getMockImplementation()!;
    http.get.mockImplementation(async (url: string, config?: { params?: { page?: number } }) => {
      if (url === "/admin/orders/42") return { data: { ...order, total: 2.5 } };
      if (url === "/admin/orders") {
        const page = config?.params?.page ?? 1;
        return { data: {
          items: [{ ...order, total: 2.5 }], page, pageSize: 50, totalItems: 51, totalPages: 2,
          hasPreviousPage: page > 1, hasNextPage: page < 2,
        } };
      }
      return defaultGet(url, config);
    });
    renderFlow();
    await buildOrder();
    fireEvent.click(screen.getByRole("tab", { name: "Orders" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Apply filters" })).toBeEnabled());
    fireEvent.change(screen.getByLabelText("Search orders"), { target: { value: "Ada" } });
    fireEvent.click(screen.getByRole("button", { name: "Apply filters" }));
    await waitFor(() => expect(orderListRequests()).toHaveLength(2));
    await waitFor(() => expect(screen.getByRole("button", { name: "Next" })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await screen.findByText("Page 2 of 2 · 51 orders");
    fireEvent.click(screen.getByRole("button", { name: "Take Payment" }));

    // Both panels stay mounted; exercise returning from a hidden Orders panel.
    fireEvent.click(screen.getByRole("tab", { name: "New Order" }));
    fireEvent.click(within(screen.getByRole("dialog", { name: "Take Payment" }))
      .getByRole("button", { name: "View Order" }));
    const card = await screen.findByRole("region", { name: "Order #42" });
    expect(screen.getByRole("tab", { name: "Orders" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tabpanel", { name: "Orders" })).toBeVisible();
    expect(card).toHaveFocus();
    expect(card).toHaveClass("r66-admin-order-target");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(document.body.style.overflow).toBe("");
    expect(http.get).toHaveBeenCalledWith("/admin/orders/42");

    fireEvent.click(screen.getByRole("button", { name: "Clear target" }));
    await screen.findByText("Page 1 of 2 · 51 orders");
    expect(screen.getByLabelText("Order status")).toHaveValue("all");
    expect(screen.getByLabelText("Search orders")).toHaveValue("");
    expect(orderListRequests().at(-1)?.[1]).toEqual({ params: { page: 1, status: "all" } });
    expect(screen.queryByRole("region", { name: "Order #42" })).not.toBeInTheDocument();
    expect(http.post).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("tab", { name: "New Order" }));
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Ada");
  });

  it("shows a successful replay as cashier confirmation with a duplicate notice", async () => {
    http.post.mockResolvedValue({ data: { Id: 42, CustomerName: "Ada" }, status: 200 });
    renderFlow();
    await buildOrder();
    submitOrder();
    await screen.findByRole("heading", { name: "Order confirmed" });
    expect(screen.getByText("Order #42")).toBeInTheDocument();
    expect(screen.getByText("This order was already submitted. No second order was created.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New Order" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Orders" })).toBeInTheDocument();
    expect(screen.getByTestId("current-path")).toHaveTextContent("/cash");
  });

  it("keeps a pending request through tab switches without duplicate submission or forced navigation", async () => {
    let resolvePost!: (value: { data: OrderDto; status: number }) => void;
    http.post.mockImplementation(() => new Promise((resolve) => { resolvePost = resolve; }));
    renderFlow();
    await buildOrder();
    submitOrder();
    const submittingButton = screen.getByRole("button", { name: "Placing order…" });
    expect(submittingButton).toBeDisabled();
    fireEvent.submit(submittingButton.closest("form")!);
    expect(http.post).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole("tab", { name: "Orders" }));
    await act(async () => { resolvePost({ data: order, status: 201 }); });
    expect(screen.getByRole("tab", { name: "Orders" })).toHaveAttribute("aria-selected", "true");
    expect(screen.queryByRole("heading", { name: "Order confirmed" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "New Order" }));
    expect(screen.getByRole("heading", { name: "Order confirmed" })).toBeInTheDocument();
    expect(screen.getByTestId("current-path")).toHaveTextContent("/cash");
  });

  it("cleans up mobile scroll locking and focus when cashier submission completes", async () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({
      matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn(),
    })));
    renderFlow();
    await buildOrder();
    expect(screen.getByRole("dialog", { name: "Order details" })).toBeInTheDocument();
    expect(document.body.style.overflow).toBe("hidden");
    submitOrder();
    const heading = await screen.findByRole("heading", { name: "Order confirmed" });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(document.body.style.overflow).toBe("");
    expect(heading).toHaveFocus();
  });

  it("logs out through the existing gate and removes the cashier draft", async () => {
    renderFlow();
    await buildOrder();
    fireEvent.click(screen.getByRole("button", { name: "Log out" }));
    expect(localStorage.getItem("token")).toBeNull();
    expect(screen.getByRole("heading", { name: "Admin Login" })).toBeInTheDocument();
    expect(screen.queryByTestId("order-item")).not.toBeInTheDocument();
    expect(screen.getByTestId("current-path")).toHaveTextContent("/cash");
  });

  it.each(["/cash", "/order"])("preserves failed drafts and retry keys on %s", async (path) => {
    http.post.mockRejectedValueOnce(new Error("Network unavailable"))
      .mockResolvedValueOnce({ data: order, status: 201 });
    vi.spyOn(console, "error").mockImplementation(() => {});
    renderFlow(path);
    await buildOrder();
    submitOrder();
    await waitFor(() => expect(toasts.error).toHaveBeenCalled());
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Ada");
    expect(screen.getByTestId("order-item")).toHaveTextContent("Espresso");
    expect(screen.getByRole("button", { name: "Place Order" })).toBeEnabled();
    if (path === "/cash") {
      fireEvent.click(screen.getByRole("tab", { name: "Orders" }));
      fireEvent.click(screen.getByRole("tab", { name: "New Order" }));
    }
    submitOrder();
    await screen.findByRole("heading", { name: path === "/cash" ? "Order confirmed" : "Order Confirmed!" });
    expect(http.post.mock.calls[1][2].headers["X-Idempotency-Key"]).toBe(
      http.post.mock.calls[0][2].headers["X-Idempotency-Key"]
    );
  });

  it.each(["/cash", "/order"])("keeps conflicts distinct from duplicate confirmations on %s", async (path) => {
    http.post.mockRejectedValueOnce({ isAxiosError: true, response: { status: 409 } })
      .mockResolvedValueOnce({ data: order, status: 201 });
    renderFlow(path);
    await buildOrder();
    submitOrder();
    await waitFor(() => expect(toasts.error).toHaveBeenCalledWith(
      "This submission key was already used for a different order. Please review the order and try again."
    ));
    expect(screen.getByTestId("current-path")).toHaveTextContent(path);
    expect(screen.getByRole("button", { name: "Place Order" })).toBeEnabled();
    expect(screen.queryByRole("heading", { name: /confirmed/i })).not.toBeInTheDocument();
    submitOrder();
    await screen.findByRole("heading", { name: path === "/cash" ? "Order confirmed" : "Order Confirmed!" });
    expect(http.post.mock.calls[1][2].headers["X-Idempotency-Key"]).not.toBe(
      http.post.mock.calls[0][2].headers["X-Idempotency-Key"]
    );
  });

  it("does not consume customer prefill or expose public status navigation inside /cash", async () => {
    renderFlow("/cash", { menuItemId: 1 });
    fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
    await screen.findByRole("button", { name: "Order Espresso" });
    expect(screen.queryByTestId("order-item")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /check order status/i })).not.toBeInTheDocument();
    expect(screen.getByTestId("current-path")).toHaveTextContent("/cash");
  });

  it("keeps public prefill, confirmation, and tracking behavior with a staff token present", async () => {
    renderFlow("/order", { menuItemId: 1 });
    await screen.findByTestId("order-item");
    expect(screen.getByRole("link", { name: /check order status/i })).toHaveAttribute("href", "/order-status");
    fireEvent.change(screen.getByRole("textbox", { name: "Name" }), { target: { value: "Ada" } });
    submitOrder();
    await screen.findByRole("heading", { name: "Order Confirmed!" });
    expect(screen.getByTestId("current-path")).toHaveTextContent("/order/confirmation");
    expect(screen.getByRole("link", { name: "Order Status" })).toHaveAttribute(
      "href", "/order-status?token=private-tracking-token"
    );
    expect(screen.queryByRole("button", { name: "New Order" })).not.toBeInTheDocument();
  });

  it("keeps public duplicate routing with a staff token present", async () => {
    http.post.mockResolvedValue({ data: order, status: 200 });
    renderFlow("/order");
    await buildOrder();
    submitOrder();
    await screen.findByRole("heading", { name: /double brew detected/i });
    expect(screen.getByTestId("current-path")).toHaveTextContent("/order/duplicate");
    expect(screen.getByRole("link", { name: "Check Order Status" })).toHaveAttribute(
      "href", "/order-status?token=private-tracking-token"
    );
  });
});
