import React from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import ViewOrders from "./ViewOrders";
import { LanguageProvider } from "../../i18n/LanguageContext";
import type { InPersonPaymentResult, OrderDto } from "../../types/api";

const http = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn() }));
vi.mock("../../axiosConfig", () => ({ default: http }));
vi.mock("react-toastify", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
const order: OrderDto = { id: 66, total: 7.25, customerName: "Mia", orderStatus: 0, orderItems: [] };
const result = (status: InPersonPaymentResult["status"], paymentId = "attempt-1"): InPersonPaymentResult => ({
  paymentId, orderId: 66, provider: "test-terminal", status, amount: 7.25, currency: "USD",
  paidUtc: status === "paid" ? "2026-10-07T10:05:00Z" : null,
});
let currentOrder: OrderDto;
let observed: InPersonPaymentResult;

async function openChooser() {
  const view = render(<LanguageProvider><ViewOrders /></LanguageProvider>);
  fireEvent.click(await screen.findByRole("button", { name: "Take Payment" }));
  vi.useFakeTimers();
  return view;
}
async function clickCard() {
  await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Card" })); });
}
async function poll() {
  await act(async () => { await vi.advanceTimersByTimeAsync(2000); });
}

describe("provider-neutral in-person Card payments", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    localStorage.setItem("roast66_locale", "en");
    currentOrder = { ...order };
    observed = result("pending");
    http.get.mockImplementation(async (url: string) => {
      if (url.startsWith("/payments/in-person/")) return { data: observed };
      if (url === "/admin/orders/new-count") return { data: { count: 0 } };
      return { data: {
        items: [currentOrder], page: 1, pageSize: 50, totalItems: 1, totalPages: 1,
        hasPreviousPage: false, hasNextPage: false,
      } };
    });
    http.post.mockResolvedValue({ data: result("pending") });
  });
  afterEach(() => {
    vi.useRealTimers();
    localStorage.removeItem("roast66_locale");
  });

  it("starts with only the order ID, blocks duplicate clicks and manual settlement, and observes Roast66", async () => {
    await openChooser();
    let finish!: (value: { data: InPersonPaymentResult }) => void;
    http.post.mockImplementation(() => new Promise((resolve) => { finish = resolve; }));
    await clickCard();
    const card = screen.getByRole("button", { name: "Card" });
    expect(card).toBeDisabled();
    fireEvent.click(card);
    expect(http.post).toHaveBeenCalledExactlyOnceWith("/payments/in-person", { orderId: 66 });
    expect(screen.getByRole("status")).toHaveTextContent("Starting card payment…");
    await act(async () => { finish({ data: result("pending") }); });
    expect(screen.getByRole("status")).toHaveTextContent("Waiting for payment…");
    expect(screen.getByRole("button", { name: "Cash" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Other" })).toBeDisabled();
    await poll();
    expect(http.get).toHaveBeenCalledWith("/payments/in-person/attempt-1");
    expect(http.put).not.toHaveBeenCalled();
    expect(screen.queryByText(/^Paid ·/)).not.toBeInTheDocument();
  });

  it("dismissal never records payment and reopening keeps the pending attempt", async () => {
    await openChooser();
    await clickCard();
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByText(/^Paid ·/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Take Payment" }));
    expect(screen.getByRole("status")).toHaveTextContent("Waiting for payment…");
    expect(screen.getByRole("button", { name: "Card" })).toBeDisabled();
    expect(http.post).toHaveBeenCalledTimes(1);
    observed = result("paid");
    currentOrder = { ...order, paidUtc: observed.paidUtc, paymentProvider: observed.provider };
    await poll();
    expect(screen.getByText("Paid · Test-terminal")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Take Payment" })).not.toBeInTheDocument();
    expect(http.post).toHaveBeenCalledTimes(1);
    const statusCalls = http.get.mock.calls.filter(([url]) => url.startsWith("/payments/in-person/"));
    await poll();
    expect(http.get.mock.calls.filter(([url]) => url.startsWith("/payments/in-person/"))).toHaveLength(statusCalls.length);
  });

  it("a confirmed failure permits a new attempt and an immediate paid response updates the order", async () => {
    await openChooser();
    await clickCard();
    observed = result("failed");
    await poll();
    expect(screen.getByRole("alert")).toHaveTextContent("Card payment failed. You can try again.");
    expect(screen.getByRole("button", { name: "Card" })).toBeEnabled();
    expect(screen.queryByText(/^Paid ·/)).not.toBeInTheDocument();
    currentOrder = { ...order, paidUtc: result("paid").paidUtc, paymentProvider: "test-terminal" };
    http.post.mockResolvedValue({ data: result("paid", "attempt-2") });
    await clickCard();
    expect(screen.getByText("Paid · Test-terminal")).toBeInTheDocument();
    expect(http.post).toHaveBeenCalledTimes(2);
  });

  it.each([503, 409])("handles start HTTP %s without claiming payment or creating a polling attempt", async (status) => {
    await openChooser();
    http.post.mockRejectedValue({ isAxiosError: true, response: { status } });
    await clickCard();
    expect(screen.getByRole("alert")).toHaveTextContent(status === 503
      ? "Card payments are unavailable. The provider is not configured or could not confirm the start."
      : "Could not start card payment. Retry to check or start the same order's payment.");
    expect(screen.queryByText(/^Paid ·/)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Card" })).toBeEnabled();
    await poll();
    expect(http.get.mock.calls.some(([url]) => url.startsWith("/payments/in-person/"))).toBe(false);
  });

  it("a status network error retains the attempt and polling recovers without another start", async () => {
    await openChooser();
    await clickCard();
    const get = http.get.getMockImplementation()!;
    http.get.mockImplementationOnce(() => Promise.reject(new Error("Network unavailable")));
    await poll();
    expect(screen.getByRole("button", { name: "Card" })).toBeDisabled();
    expect(screen.getByRole("alert")).toHaveTextContent("Could not check payment status. Retrying…");
    http.get.mockImplementation(get);
    await poll();
    expect(within(screen.getByRole("dialog")).getByRole("status")).toHaveTextContent("Waiting for payment…");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(http.post).toHaveBeenCalledTimes(1);
  });

  it("a paid result for another order is ignored and keeps the current attempt pending", async () => {
    await openChooser();
    await clickCard();
    observed = { ...result("paid"), orderId: 67 };
    await poll();
    expect(screen.queryByText(/^Paid ·/)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Card" })).toBeDisabled();
    expect(screen.getByRole("alert")).toHaveTextContent("Could not check payment status. Retrying…");
  });

  it("unmount stops status observation without sending a cancellation or recording payment", async () => {
    const view = await openChooser();
    await clickCard();
    view.unmount();
    await poll();
    expect(http.get.mock.calls.some(([url]) => url.startsWith("/payments/in-person/"))).toBe(false);
    expect(http.post).toHaveBeenCalledExactlyOnceWith("/payments/in-person", { orderId: 66 });
  });
});
