import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import CategoryType from "../constants/categories";
import { LanguageProvider } from "../i18n/LanguageContext";

const mockGet = vi.fn();
const mockPost = vi.fn();

vi.mock("../axiosConfig", () => ({
  default: {
    get: (...args: unknown[]) => mockGet(...args),
    post: (...args: unknown[]) => mockPost(...args),
  },
}));

const toastFns = vi.hoisted(() => ({
  warning: vi.fn(),
  error: vi.fn(),
  info: vi.fn(),
  success: vi.fn(),
}));

vi.mock("react-toastify", () => ({
  toast: {
    warning: (...args: unknown[]) => toastFns.warning(...args),
    error: (...args: unknown[]) => toastFns.error(...args),
    info: (...args: unknown[]) => toastFns.info(...args),
    success: (...args: unknown[]) => toastFns.success(...args),
  },
}));

const mockNavigate = vi.fn();

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

import OrderPage from "./OrderPage";

const menuPayload = [
  {
    id: 1,
    name: "Espresso",
    price: 2.5,
    description: "Strong coffee",
    categoryType: CategoryType.COFFEE,
  },
];

const menuPayloadWithFlavor = [
  ...menuPayload,
  {
    id: 2,
    name: "Vanilla",
    price: 0.5,
    description: "Sweet flavor",
    categoryType: CategoryType.FLAVORS,
  },
];

function renderOrderPage(initialEntry: { pathname: string; state?: Record<string, unknown> }) {
  return render(
    <LanguageProvider>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/order" element={<OrderPage />} />
        </Routes>
      </MemoryRouter>
    </LanguageProvider>
  );
}

describe("OrderPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("prefills cart from navigation state menuItemId after menu loads", async () => {
    mockGet.mockResolvedValue({ data: menuPayload });

    renderOrderPage({ pathname: "/order", state: { menuItemId: 1 } });

    await waitFor(() => {
      expect(document.querySelectorAll(".order-item")).toHaveLength(1);
    });
    expect(document.querySelector(".order-item")).toHaveTextContent("Espresso");

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith("/order", {
        replace: true,
        state: {},
      });
    });
  });

  it("does not prefill when menuItemId is missing from state", async () => {
    mockGet.mockResolvedValue({ data: menuPayload });

    renderOrderPage({ pathname: "/order", state: {} });

    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/menu");
    });

    expect(document.querySelectorAll(".order-item")).toHaveLength(0);
  });

  it("clears prefill state when menuItemId does not match any menu item", async () => {
    mockGet.mockResolvedValue({ data: menuPayload });

    renderOrderPage({ pathname: "/order", state: { menuItemId: 999 } });

    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/menu");
    });

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith("/order", {
        replace: true,
        state: {},
      });
    });

    expect(document.querySelectorAll(".order-item")).toHaveLength(0);
  });

  it("does not prefill flavor items and shows a warning", async () => {
    mockGet.mockResolvedValue({ data: menuPayloadWithFlavor });

    renderOrderPage({ pathname: "/order", state: { menuItemId: 2 } });

    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith("/menu");
    });

    await waitFor(() => {
      expect(toastFns.warning).toHaveBeenCalled();
    });

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith("/order", {
        replace: true,
        state: {},
      });
    });

    expect(document.querySelectorAll(".order-item")).toHaveLength(0);
  });
});
