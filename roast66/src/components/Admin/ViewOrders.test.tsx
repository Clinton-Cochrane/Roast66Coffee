import React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import ViewOrders from "./ViewOrders";
import { LanguageProvider } from "../../i18n/LanguageContext";
import type { OrderDto } from "../../types/api";

const mockGet = vi.fn();
const mockPut = vi.fn();

vi.mock("../../axiosConfig", () => ({
  default: {
    get: (...args: unknown[]) => mockGet(...args),
    put: (...args: unknown[]) => mockPut(...args),
  },
}));

vi.mock("react-toastify", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const completedOrder: OrderDto = {
  id: 66,
  customerName: "Mia",
  orderDate: "2026-08-26T10:00:00Z",
  orderStatus: 3,
  orderItems: [
    {
      id: 1,
      quantity: 1,
      menuItem: { name: "Blue Flame Nitro" },
      addOns: [
        { id: 2, quantity: 2, menuItem: { name: "Blueberry Shot" } },
        { id: 3, quantity: 1, menuItem: { name: "Vanilla Shot" } },
      ],
    },
  ],
};

const pageResponse = (items: OrderDto[], page = 1, totalItems = items.length) => ({
  items,
  page,
  pageSize: 50,
  totalItems,
  totalPages: totalItems === 0 ? 0 : Math.ceil(totalItems / 50),
  hasPreviousPage: page > 1,
  hasNextPage: page * 50 < totalItems,
});

describe("ViewOrders", () => {
  beforeEach(() => {
    mockGet.mockReset();
    mockPut.mockReset();
    mockGet.mockResolvedValue({ data: pageResponse([completedOrder]) });
    mockPut.mockResolvedValue({ data: { newStatus: "Preparing", changed: true } });
  });

  it("mutes completed order copy while leaving completed status and buttons outside it", async () => {
    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    const title = await screen.findByRole("heading", { name: "Order #66" });
    const card = title.parentElement;
    const completedCopy = card?.querySelector(".r66-admin-completed-copy");

    expect(card).toHaveClass("r66-admin-completed-order");
    expect(completedCopy).toContainElement(screen.getByText("Mia"));
    expect(completedCopy).toContainElement(screen.getByText("Blue Flame Nitro"));
    expect(completedCopy).toHaveTextContent("Shots: Blueberry Shot × 2, Vanilla Shot × 1");
    expect(completedCopy).not.toContainElement(screen.getByText("Completed — no further action"));
    expect(completedCopy).not.toContainElement(
      screen.getByRole("button", { name: "Refresh notifications" })
    );
  });

  it("preserves the deterministic order supplied by the paginated API", async () => {
    mockGet.mockResolvedValue({
      data: pageResponse([
        {
          ...completedOrder,
          id: 4,
          orderDate: "2026-08-26T12:00:00Z",
          orderStatus: 1,
        },
        {
          ...completedOrder,
          id: 2,
          orderDate: "2026-08-26T09:00:00Z",
          orderStatus: 0,
        },
        {
          ...completedOrder,
          id: 3,
          orderDate: "2026-08-26T13:00:00Z",
        },
        {
          ...completedOrder,
          id: 1,
          orderDate: "2026-08-26T08:00:00Z",
        },
      ]),
    });

    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    const orderHeadings = await screen.findAllByRole("heading", { level: 2 });
    expect(orderHeadings.map((heading) => heading.textContent)).toEqual([
      "Order #4",
      "Order #2",
      "Order #3",
      "Order #1",
    ]);
  });

  it("labels a Stripe-settled order as paid", async () => {
    mockGet.mockResolvedValue({
      data: pageResponse([
        {
          ...completedOrder,
          paidUtc: "2026-08-26T10:05:00Z",
          paymentProvider: "stripe",
        },
      ]),
    });

    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    expect(await screen.findByText("Paid · Stripe")).toBeInTheDocument();
  });

  it("does not expose a manual order deletion control", async () => {
    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    await screen.findByRole("heading", { name: "Order #66" });

    expect(screen.queryByRole("button", { name: /delete/i })).not.toBeInTheDocument();
  });

  it("sends the displayed status and blocks duplicate clicks while advancing", async () => {
    let resolveUpdate!: (value: { data: { newStatus: string; changed: boolean } }) => void;
    mockGet.mockResolvedValue({
      data: pageResponse([{ ...completedOrder, orderStatus: 1 }]),
    });
    mockPut.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveUpdate = resolve;
        })
    );

    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    const advance = await screen.findByRole("button", { name: "Advance status" });
    fireEvent.click(advance);

    expect(mockPut).toHaveBeenCalledWith("/admin/updateOrderStatus/66/status", {
      expectedStatus: 1,
    });
    expect(advance).toBeDisabled();
    fireEvent.click(advance);
    expect(mockPut).toHaveBeenCalledTimes(1);

    resolveUpdate({ data: { newStatus: "ReadyForPickup", changed: true } });
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2));
  });

  it("shows no advance action for completed or unknown statuses", async () => {
    mockGet.mockResolvedValue({
      data: pageResponse([completedOrder, { ...completedOrder, id: 67, orderStatus: 99 }]),
    });

    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    await screen.findByRole("heading", { name: "Order #67" });
    expect(screen.queryByRole("button", { name: /advance status|mark complete/i })).not.toBeInTheDocument();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });

  it("submits status and drink-name search filters and resets to page one", async () => {
    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    await screen.findByRole("heading", { name: "Order #66" });
    fireEvent.change(screen.getByLabelText("Order status"), { target: { value: "completed" } });
    fireEvent.change(screen.getByLabelText("Search orders"), { target: { value: "Superman" } });
    fireEvent.click(screen.getByRole("button", { name: "Apply filters" }));

    await waitFor(() =>
      expect(mockGet).toHaveBeenCalledWith("/admin/orders", {
        params: expect.objectContaining({ page: 1, status: "completed", search: "Superman" }),
      })
    );
  });

  it("requests the next fixed-size page", async () => {
    mockGet.mockResolvedValue({ data: pageResponse([completedOrder], 1, 51) });
    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    expect(await screen.findByText("Page 1 of 2 · 51 orders")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Next" }));

    await waitFor(() =>
      expect(mockGet).toHaveBeenCalledWith("/admin/orders", {
        params: expect.objectContaining({ page: 2 }),
      })
    );
  });

  it("shows loading and filtered-empty states", async () => {
    let resolveRequest!: (value: { data: ReturnType<typeof pageResponse> }) => void;
    mockGet.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveRequest = resolve;
        })
    );
    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    expect(screen.getByText("Loading orders...")).toBeInTheDocument();
    resolveRequest({ data: pageResponse([]) });

    expect(await screen.findByText("No orders match these filters.")).toBeInTheDocument();
  });

  it("returns to the last valid page when retention removes the current page", async () => {
    let retentionExpired = false;
    const secondPageOrder = { ...completedOrder, id: 67 };
    mockGet.mockImplementation((url: string, config?: { params?: { page?: number } }) => {
      if (url.includes("new-count")) return Promise.resolve({ data: { count: 0 } });
      const requestedPage = config?.params?.page ?? 1;
      if (requestedPage === 2) {
        return Promise.resolve({
          data: retentionExpired
            ? { ...pageResponse([], 2, 50), totalPages: 1, hasPreviousPage: true }
            : pageResponse([secondPageOrder], 2, 51),
        });
      }
      return Promise.resolve({
        data: pageResponse([completedOrder], 1, retentionExpired ? 50 : 51),
      });
    });

    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    await screen.findByText("Page 1 of 2 · 51 orders");
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await screen.findByRole("heading", { name: "Order #67" });

    retentionExpired = true;
    fireEvent.click(screen.getByRole("button", { name: "Refresh" }));

    expect(await screen.findByText("Page 1 of 1 · 50 orders")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Order #67" })).not.toBeInTheDocument();
  });
});
