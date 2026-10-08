import React from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { toast } from "react-toastify";
import ViewOrders from "./ViewOrders";
import { LanguageProvider } from "../../i18n/LanguageContext";
import { ORDER_STATUS } from "../../constants/orderStatus";
import type { OrderDto } from "../../types/api";

const http = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn() }));
vi.mock("../../axiosConfig", () => ({ default: http }));
vi.mock("react-toastify", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const unpaidOrder: OrderDto = {
  id: 66, customerName: "Mia", total: 7.25, orderStatus: ORDER_STATUS.Received,
  orderDate: "2026-08-26T10:00:00Z", orderItems: [], paidUtc: null,
};
const receipt = (method: string, wasReplay = false) => ({
  paymentId: "manual-receipt", orderId: 66, method, amount: 7.25, currency: "USD",
  paidUtc: "2026-08-26T10:05:00Z", wasReplay,
});
const page = (order: OrderDto) => ({
  items: [order], page: 1, pageSize: 50, totalItems: 1, totalPages: 1,
  hasPreviousPage: false, hasNextPage: false,
});
function showOrders(targetOrderId?: number) {
  render(<LanguageProvider><ViewOrders targetOrderId={targetOrderId} /></LanguageProvider>);
}
async function choose(label = "Cash") {
  fireEvent.click(await screen.findByRole("button", { name: "Take Payment" }));
  fireEvent.click(screen.getByRole("button", { name: label }));
}
function confirm() {
  fireEvent.click(screen.getByRole("button", { name: "Confirm payment received" }));
}

describe("staff manual payment recording", () => {
  afterEach(() => localStorage.removeItem("roast66_locale"));
  beforeEach(() => {
    vi.clearAllMocks();
    http.get.mockReset();
    http.post.mockReset();
    localStorage.setItem("roast66_locale", "en");
    http.get.mockImplementation((url: string) => Promise.resolve({
      data: url === "/admin/orders/new-count" ? { count: 0 } : page(unpaidOrder),
    }));
  });

  const scenarios = Object.entries(ORDER_STATUS).flatMap(([status, orderStatus]) =>
    ["Cash", "Other"].map((label) => ({ status, orderStatus, label })));
  it.each(scenarios)("records $label independently of $status fulfillment", async ({ orderStatus, label }) => {
    let order = { ...unpaidOrder, orderStatus };
    http.get.mockImplementation((url: string) => Promise.resolve({
      data: url === "/admin/orders/new-count" ? { count: 0 } : page(order),
    }));
    http.post.mockImplementation((_url: string, body: { method: string }) => {
      order = { ...order, paidUtc: receipt(body.method).paidUtc, paymentProvider: body.method };
      return Promise.resolve({ data: receipt(body.method) });
    });
    showOrders();
    await choose(label);
    expect(http.post).not.toHaveBeenCalled();
    expect(screen.queryByText(`Paid · ${label}`)).not.toBeInTheDocument();
    confirm();
    expect(await screen.findByText(`Paid · ${label}`)).toBeInTheDocument();
    expect(http.post).toHaveBeenCalledExactlyOnceWith("/payments/manual", { orderId: 66, method: label.toLowerCase() });
    expect(http.put).not.toHaveBeenCalled();
    expect(order.orderStatus).toBe(orderStatus);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Take Payment" })).not.toBeInTheDocument();
    await waitFor(() => expect(http.get.mock.calls.filter(([url]) => url === "/admin/orders")).toHaveLength(2));
  });

  it("updates an exactly targeted order and refreshes that same ID", async () => {
    let order = unpaidOrder;
    http.get.mockImplementation(() => Promise.resolve({ data: order }));
    http.post.mockImplementation(() => {
      order = { ...order, paidUtc: receipt("other").paidUtc, paymentProvider: "other" };
      return Promise.resolve({ data: receipt("other") });
    });
    showOrders(66);
    await choose("Other");
    confirm();
    expect(await screen.findByText("Paid · Other")).toBeInTheDocument();
    await waitFor(() => expect(http.get.mock.calls.filter(([url]) => url === "/admin/orders/66")).toHaveLength(2));
    expect(http.get.mock.calls.some(([url]) => url === "/admin/orders")).toBe(false);
  });

  it("blocks repeated clicks and reopening during recording, then applies success after dismissal", async () => {
    let finish!: (value: { data: ReturnType<typeof receipt> }) => void;
    http.post.mockImplementation(() => new Promise((resolve) => { finish = resolve; }));
    showOrders();
    await choose();
    confirm();
    const pending = screen.getByRole("button", { name: "Recording payment…" });
    expect(pending).toBeDisabled();
    fireEvent.click(pending);
    expect(screen.getByRole("button", { name: "Back" })).toBeDisabled();
    fireEvent.keyDown(document, { key: "Escape" });
    fireEvent.click(screen.getByRole("button", { name: "Take Payment" }));
    expect(screen.getByRole("button", { name: "Cash" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Other" })).toBeDisabled();
    http.get.mockRejectedValue(new Error("Refresh unavailable"));
    await act(async () => finish({ data: receipt("cash") }));
    expect(await screen.findByText("Paid · Cash")).toBeInTheDocument();
    expect(http.post).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "View Orders" })).toHaveFocus();
  });

  it("keeps an unpaid order retryable on failure and accepts a replay receipt", async () => {
    http.post.mockRejectedValueOnce(new Error("Network unavailable"));
    showOrders();
    await choose();
    confirm();
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Could not confirm payment. Retry the same method to safely check or record it."));
    expect(screen.queryByText(/^Paid ·/)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Confirm payment received" })).toBeEnabled();
    http.post.mockResolvedValueOnce({ data: receipt("cash", true) });
    http.get.mockRejectedValue(new Error("Refresh unavailable"));
    confirm();
    expect(await screen.findByText("Paid · Cash")).toBeInTheDocument();
    expect(http.post).toHaveBeenCalledTimes(2);
  });

  it.each([401, 403, 404])("handles HTTP %s without claiming this payment succeeded", async (status) => {
    http.post.mockRejectedValue({ isAxiosError: true, response: { status } });
    showOrders();
    await choose("Other");
    confirm();
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect(screen.queryByText("Paid · Other")).not.toBeInTheDocument();
    expect(toast.success).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Confirm payment received" })).toBeEnabled();
  });

  it("refreshes a conflict to show the payment already recorded by another source", async () => {
    http.post.mockRejectedValue({ isAxiosError: true, response: { status: 409 } });
    showOrders();
    await choose("Other");
    http.get.mockResolvedValue({ data: page({ ...unpaidOrder, paidUtc: receipt("stripe").paidUtc, paymentProvider: "stripe" }) });
    confirm();
    expect(await screen.findByText("Paid · Stripe")).toBeInTheDocument();
    expect(screen.queryByText("Paid · Other")).not.toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(toast.success).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith("This order already has a recorded or pending payment. Refreshing its payment status.");
  });

  it("ignores an unpaid refresh begun before payment success", async () => {
    let finishRead!: (value: { data: ReturnType<typeof page> }) => void;
    let finishPayment!: (value: { data: ReturnType<typeof receipt> }) => void;
    http.post.mockImplementation(() => new Promise((resolve) => { finishPayment = resolve; }));
    showOrders();
    await choose();
    confirm();
    fireEvent.keyDown(document, { key: "Escape" });
    http.get.mockImplementation((url: string) => url === "/admin/orders"
      ? new Promise((resolve) => { finishRead = resolve; }) : Promise.resolve({ data: { count: 0 } }));
    fireEvent.click(screen.getByRole("button", { name: "Refresh" }));
    http.get.mockRejectedValue(new Error("Refresh unavailable"));
    await act(async () => finishPayment({ data: receipt("cash") }));
    await act(async () => finishRead({ data: page(unpaidOrder) }));
    expect(await screen.findByText("Paid · Cash")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Take Payment" })).not.toBeInTheDocument();
  });

  it("refreshes the current target when payment finishes after switching to another order", async () => {
    let finish!: (value: { data: ReturnType<typeof receipt> }) => void;
    http.post.mockImplementation(() => new Promise((resolve) => { finish = resolve; }));
    http.get.mockImplementation((url: string) => Promise.resolve({
      data: { ...unpaidOrder, id: Number(url.split("/").at(-1)) },
    }));
    const { rerender } = render(<LanguageProvider><ViewOrders targetOrderId={66} /></LanguageProvider>);
    await choose();
    confirm();
    fireEvent.keyDown(document, { key: "Escape" });
    rerender(<LanguageProvider><ViewOrders targetOrderId={67} /></LanguageProvider>);
    await screen.findByRole("heading", { name: "Order #67" });
    fireEvent.click(await screen.findByRole("button", { name: "Take Payment" }));
    await act(async () => finish({ data: receipt("cash") }));
    await waitFor(() => expect(http.get.mock.calls.filter(([url]) => url === "/admin/orders/67")).toHaveLength(2));
    expect(screen.getByRole("dialog")).toHaveTextContent("Order #67");
    expect(screen.getByRole("button", { name: "Cash" })).toBeEnabled();
    expect(screen.queryByText("Paid · Cash")).not.toBeInTheDocument();
    expect(http.post).toHaveBeenCalledExactlyOnceWith("/payments/manual", { orderId: 66, method: "cash" });
  });
});
