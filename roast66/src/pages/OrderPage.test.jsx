import React from "react";
import { render, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import CategoryType from "../constants/categories";
import { LanguageProvider } from "../i18n/LanguageContext";

jest.mock("../axiosConfig", () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
    post: jest.fn(),
  },
}));

jest.mock("react-toastify", () => ({
  toast: {
    warning: jest.fn(),
    error: jest.fn(),
    info: jest.fn(),
    success: jest.fn(),
  },
}));

const mockNavigate = jest.fn();

jest.mock("react-router-dom", () => ({
  __esModule: true,
  ...jest.requireActual("react-router-dom"),
  useNavigate: () => mockNavigate,
}));

import axios from "../axiosConfig";
import { toast } from "react-toastify";
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

function renderOrderPage(initialEntry) {
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
    jest.clearAllMocks();
  });

  it("prefills cart from navigation state menuItemId after menu loads", async () => {
    axios.get.mockResolvedValue({ data: menuPayload });

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
    axios.get.mockResolvedValue({ data: menuPayload });

    renderOrderPage({ pathname: "/order", state: {} });

    await waitFor(() => {
      expect(axios.get).toHaveBeenCalledWith("/menu");
    });

    expect(document.querySelectorAll(".order-item")).toHaveLength(0);
  });

  it("clears prefill state when menuItemId does not match any menu item", async () => {
    axios.get.mockResolvedValue({ data: menuPayload });

    renderOrderPage({ pathname: "/order", state: { menuItemId: 999 } });

    await waitFor(() => {
      expect(axios.get).toHaveBeenCalledWith("/menu");
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
    axios.get.mockResolvedValue({ data: menuPayloadWithFlavor });

    renderOrderPage({ pathname: "/order", state: { menuItemId: 2 } });

    await waitFor(() => {
      expect(axios.get).toHaveBeenCalledWith("/menu");
    });

    await waitFor(() => {
      expect(toast.warning).toHaveBeenCalled();
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
