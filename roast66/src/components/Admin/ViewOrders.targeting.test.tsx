import React, { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import ViewOrders from "./ViewOrders";
import { LanguageProvider } from "../../i18n/LanguageContext";
import type { OrderDto } from "../../types/api";

const http = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn() }));
vi.mock("../../axiosConfig", () => ({ default: http }));
vi.mock("react-toastify", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const targetOrder: OrderDto = {
  id: 66, customerName: "Mia", orderStatus: 3, total: 7.25,
  orderDate: "2026-08-26T10:00:00Z",
  orderItems: [{ itemName: "Saved latte", quantity: 2 }],
};
const otherOrder: OrderDto = { ...targetOrder, id: 666, customerName: "Order 66 lookalike" };
const pageResponse = (page = 1) => ({
  items: [otherOrder], page, pageSize: 50, totalItems: 51, totalPages: 2,
  hasPreviousPage: page > 1, hasNextPage: page < 2,
});
const scrollIntoView = vi.fn();
const originalScroll = Object.getOwnPropertyDescriptor(HTMLElement.prototype, "scrollIntoView");

function OrdersHost() {
  const [targetOrderId, setTargetOrderId] = useState<number | null>(null);
  return <LanguageProvider>
    <button onClick={() => setTargetOrderId(66)}>Target order 66</button>
    <button onClick={() => setTargetOrderId(67)}>Target order 67</button>
    <ViewOrders targetOrderId={targetOrderId} onTargetOrderChange={setTargetOrderId} />
  </LanguageProvider>;
}

describe("exact-order targeting", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    localStorage.setItem("roast66_locale", "en");
    Object.defineProperty(HTMLElement.prototype, "scrollIntoView", { configurable: true, value: scrollIntoView });
    http.get.mockImplementation(async (url: string, config?: { params?: { page?: number } }) => {
      if (url === "/admin/orders/new-count") return { data: { count: 0 } };
      if (url === "/admin/orders") return { data: pageResponse(config?.params?.page) };
      if (url === "/admin/orders/66") return { data: targetOrder };
      if (url === "/admin/orders/67") return { data: { ...targetOrder, id: 67 } };
      if (url === "/admin/orders/666") return { data: otherOrder };
      throw new Error(`Unexpected GET ${url}`);
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    localStorage.removeItem("roast66_locale");
    if (originalScroll) Object.defineProperty(HTMLElement.prototype, "scrollIntoView", originalScroll);
    else Reflect.deleteProperty(HTMLElement.prototype, "scrollIntoView");
  });

  it("bypasses page, status, text, and date filters and clearing restores the default first page", async () => {
    const { container } = render(<OrdersHost />);
    await screen.findByText("Page 1 of 2 · 51 orders");
    fireEvent.change(screen.getByLabelText("Order status"), { target: { value: "received" } });
    fireEvent.change(screen.getByLabelText("Search orders"), { target: { value: "666" } });
    fireEvent.change(screen.getByLabelText("Ordered from"), { target: { value: "2026-09-01" } });
    fireEvent.change(screen.getByLabelText("Ordered through"), { target: { value: "2026-09-02" } });
    fireEvent.click(screen.getByRole("button", { name: "Apply filters" }));
    await waitFor(() => expect(http.get).toHaveBeenCalledWith("/admin/orders", {
      params: expect.objectContaining({ status: "received", search: "666", fromUtc: expect.any(String), toUtc: expect.any(String) }),
    }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Next" })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await screen.findByText("Page 2 of 2 · 51 orders");

    fireEvent.click(screen.getByRole("button", { name: "Target order 66" }));
    const card = await screen.findByRole("region", { name: "Order #66" });
    expect(http.get).toHaveBeenCalledWith("/admin/orders/66");
    expect(screen.queryByRole("heading", { name: "Order #666" })).not.toBeInTheDocument();
    expect(within(card).getByText("Saved latte")).toBeInTheDocument();
    expect(card).toHaveFocus();
    expect(card).toHaveClass("r66-admin-order-target");
    expect(scrollIntoView).toHaveBeenCalledWith({ block: "center", behavior: "auto" });
    expect(scrollIntoView.mock.contexts[0]).toBe(card);
    expect(screen.queryByRole("navigation", { name: "Order history pages" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Clear target" }));
    await screen.findByText("Page 1 of 2 · 51 orders");
    expect(http.get.mock.calls.filter(([url]) => url === "/admin/orders").at(-1)?.[1])
      .toEqual({ params: { page: 1, status: "all" } });
    expect(screen.getByLabelText("Order status")).toHaveValue("all");
    expect(screen.getByLabelText("Search orders")).toHaveValue("");
    expect(screen.getByLabelText("Ordered from")).toHaveValue("");
    expect(screen.getByLabelText("Ordered through")).toHaveValue("");
    expect(container.querySelector(".r66-admin-order-target")).toBeNull();
    expect(screen.queryByRole("region", { name: "Order #66" })).not.toBeInTheDocument();
    expect(http.post).not.toHaveBeenCalled();
    expect(http.put).not.toHaveBeenCalled();
  });

  it("View Order closes the chooser, targets its exact ID, and leaves payment untouched", async () => {
    render(<OrdersHost />);
    fireEvent.click(await screen.findByRole("button", { name: "Take Payment" }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "View Order" }));

    await waitFor(() => expect(http.get).toHaveBeenCalledWith("/admin/orders/666"));
    expect(await screen.findByRole("region", { name: "Order #666" })).toHaveFocus();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(document.body.style.overflow).toBe("");
    expect(http.post).not.toHaveBeenCalled();
    expect(http.put).not.toHaveBeenCalled();
  });

  it("View Order can target the same order again after it is already displayed", async () => {
    render(<OrdersHost />);
    await screen.findByRole("heading", { name: "Order #666" });
    fireEvent.click(screen.getByRole("button", { name: "Target order 66" }));
    const initialCard = await screen.findByRole("region", { name: "Order #66" });
    const trigger = within(initialCard).getByRole("button", { name: "Take Payment" });
    fireEvent.click(trigger);
    fireEvent.keyDown(document, { key: "Escape" });
    expect(trigger).toHaveFocus();
    expect(scrollIntoView).toHaveBeenCalledTimes(1);
    fireEvent.click(trigger);
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "View Order" }));

    const card = await screen.findByRole("region", { name: "Order #66" });
    expect(card).toHaveFocus();
    expect(scrollIntoView).toHaveBeenCalledTimes(2);
    expect(http.get.mock.calls.filter(([url]) => url === "/admin/orders/66")).toHaveLength(2);
  });

  it("refreshes the current target when an earlier fulfillment update finishes", async () => {
    const get = http.get.getMockImplementation()!;
    let exactOrder = { ...otherOrder, orderStatus: 0 };
    let resolveUpdate!: (value: { data: { newStatus: string } }) => void;
    http.put.mockImplementation(() => new Promise((resolve) => { resolveUpdate = resolve; }));
    http.get.mockImplementation(async (url: string, ...args: unknown[]) => {
      if (url === "/admin/orders") return { data: { ...pageResponse(), items: [exactOrder] } };
      if (url === "/admin/orders/666") return { data: exactOrder };
      return get(url, ...args);
    });
    render(<OrdersHost />);
    fireEvent.click(await screen.findByRole("button", { name: "Advance status" }));
    fireEvent.click(screen.getByRole("button", { name: "Take Payment" }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "View Order" }));
    const card = await screen.findByRole("region", { name: "Order #666" });

    exactOrder = { ...exactOrder, orderStatus: 1 };
    await act(async () => { resolveUpdate({ data: { newStatus: "Preparing" } }); });
    expect(within(card).getByText("Preparing your drinks")).toBeInTheDocument();
    expect(http.get.mock.calls.filter(([url]) => url === "/admin/orders/666")).toHaveLength(2);
    expect(http.get.mock.calls.filter(([url]) => url === "/admin/orders")).toHaveLength(1);
    expect(http.post).not.toHaveBeenCalled();
  });

  it("waits for Orders to be visible before focusing and scrolling", async () => {
    const onTargetOrderChange = vi.fn();
    const { rerender } = render(<LanguageProvider><div hidden>
      <ViewOrders targetOrderId={66} onTargetOrderChange={onTargetOrderChange} isActive={false} />
    </div></LanguageProvider>);
    await screen.findByText("Saved latte");
    expect(scrollIntoView).not.toHaveBeenCalled();

    rerender(<LanguageProvider><div>
      <ViewOrders targetOrderId={66} onTargetOrderChange={onTargetOrderChange} isActive />
    </div></LanguageProvider>);
    expect(screen.getByRole("region", { name: "Order #66" })).toHaveFocus();
    expect(scrollIntoView).toHaveBeenCalledTimes(1);
    expect(http.get.mock.calls.filter(([url]) => url === "/admin/orders/66")).toHaveLength(1);
  });

  it("removes the temporary highlight after five seconds while keeping the target", async () => {
    vi.useFakeTimers();
    await act(async () => { render(<OrdersHost />); });
    await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Target order 66" })); });
    const card = screen.getByRole("region", { name: "Order #66" });
    expect(card).toHaveClass("r66-admin-order-target");
    act(() => { vi.advanceTimersByTime(5000); });
    expect(card).not.toHaveClass("r66-admin-order-target");
    expect(card).toHaveFocus();
    expect(screen.getByRole("button", { name: "Clear target" })).toBeInTheDocument();
  });

  it.each([
    { status: 404, message: "Order #66 was not found." },
    { status: 500, message: "Could not load Order #66. Try Refresh or clear the target." },
  ])("shows a recoverable error for an exact lookup returning $status", async ({ status, message }) => {
    const get = http.get.getMockImplementation()!;
    http.get.mockImplementation((url: string, ...args: unknown[]) => url === "/admin/orders/66"
      ? Promise.reject({ isAxiosError: true, response: { status } }) : get(url, ...args));
    render(<OrdersHost />);
    await screen.findByRole("heading", { name: "Order #666" });
    fireEvent.click(screen.getByRole("button", { name: "Target order 66" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(message);
    expect(screen.queryByRole("heading", { name: "Order #666" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Clear target" })).toBeEnabled();

    http.get.mockImplementation(get);
    await waitFor(() => expect(screen.getByRole("button", { name: "Refresh" })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: "Refresh" }));
    expect(await screen.findByRole("region", { name: "Order #66" })).toHaveFocus();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("rejects a lookalike ID instead of surfacing the wrong order", async () => {
    const get = http.get.getMockImplementation()!;
    http.get.mockImplementation((url: string, ...args: unknown[]) => url === "/admin/orders/66"
      ? Promise.resolve({ data: otherOrder }) : get(url, ...args));
    render(<OrdersHost />);
    await screen.findByRole("heading", { name: "Order #666" });
    fireEvent.click(screen.getByRole("button", { name: "Target order 66" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Could not load Order #66.");
    expect(screen.queryByRole("heading", { name: "Order #666" })).not.toBeInTheDocument();
    expect(scrollIntoView).not.toHaveBeenCalled();
  });

  it("ignores a pending exact lookup after the target is cleared", async () => {
    const get = http.get.getMockImplementation()!;
    let resolveTarget!: (value: { data: OrderDto }) => void;
    http.get.mockImplementation((url: string, ...args: unknown[]) => url === "/admin/orders/66"
      ? new Promise((resolve) => { resolveTarget = resolve; }) : get(url, ...args));
    render(<OrdersHost />);
    await screen.findByRole("heading", { name: "Order #666" });
    fireEvent.click(screen.getByRole("button", { name: "Target order 66" }));
    expect(screen.getByText("Loading order #66...")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Clear target" }));
    await screen.findByRole("heading", { name: "Order #666" });
    await act(async () => { resolveTarget({ data: targetOrder }); });
    expect(screen.queryByRole("region", { name: "Order #66" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Clear target" })).not.toBeInTheDocument();
    expect(scrollIntoView).not.toHaveBeenCalled();
  });

  it("ignores an older target response after another exact ID is selected", async () => {
    const get = http.get.getMockImplementation()!;
    let resolveTarget!: (value: { data: OrderDto }) => void;
    http.get.mockImplementation((url: string, ...args: unknown[]) => url === "/admin/orders/66"
      ? new Promise((resolve) => { resolveTarget = resolve; }) : get(url, ...args));
    render(<OrdersHost />);
    await screen.findByRole("heading", { name: "Order #666" });
    fireEvent.click(screen.getByRole("button", { name: "Target order 66" }));
    fireEvent.click(screen.getByRole("button", { name: "Target order 67" }));
    const card = await screen.findByRole("region", { name: "Order #67" });
    await act(async () => { resolveTarget({ data: targetOrder }); });
    expect(card).toHaveFocus();
    expect(screen.queryByRole("region", { name: "Order #66" })).not.toBeInTheDocument();
  });
});
