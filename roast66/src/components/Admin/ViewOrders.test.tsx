import React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import ViewOrders from "./ViewOrders";
import { LanguageProvider } from "../../i18n/LanguageContext";
import type { OrderDto } from "../../types/api";

const mockGet = vi.fn();

vi.mock("../../axiosConfig", () => ({
  default: {
    get: (...args: unknown[]) => mockGet(...args),
    put: vi.fn(),
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

describe("ViewOrders", () => {
  beforeEach(() => {
    mockGet.mockReset();
    mockGet.mockResolvedValue({ data: [completedOrder] });
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

  it("sorts incomplete orders FIFO before completed orders FIFO", async () => {
    mockGet.mockResolvedValue({
      data: [
        {
          ...completedOrder,
          id: 3,
          orderDate: "2026-08-26T13:00:00Z",
        },
        {
          ...completedOrder,
          id: 4,
          orderDate: "2026-08-26T12:00:00Z",
          orderStatus: 1,
        },
        {
          ...completedOrder,
          id: 1,
          orderDate: "2026-08-26T08:00:00Z",
        },
        {
          ...completedOrder,
          id: 2,
          orderDate: "2026-08-26T09:00:00Z",
          orderStatus: 0,
        },
      ],
    });

    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    const orderHeadings = await screen.findAllByRole("heading", { level: 2 });
    expect(orderHeadings.map((heading) => heading.textContent)).toEqual([
      "Order #2",
      "Order #4",
      "Order #1",
      "Order #3",
    ]);
  });

  it("labels a Stripe-settled order as paid", async () => {
    mockGet.mockResolvedValue({
      data: [
        {
          ...completedOrder,
          paidUtc: "2026-08-26T10:05:00Z",
          paymentProvider: "stripe",
        },
      ],
    });

    render(
      <LanguageProvider>
        <ViewOrders />
      </LanguageProvider>
    );

    expect(await screen.findByText("Paid · Stripe")).toBeInTheDocument();
  });
});
