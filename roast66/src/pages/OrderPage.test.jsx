import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import CategoryType from "../constants/categories";
import { LanguageProvider } from "../i18n/LanguageContext";
import OrderPage from "./OrderPage";

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

import axios from "../axiosConfig";

const menuPayload = [
  {
    id: 1,
    name: "Espresso",
    price: 2.5,
    description: "Strong coffee",
    categoryType: CategoryType.COFFEE,
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
  });

  it("does not prefill when menuItemId is missing from state", async () => {
    axios.get.mockResolvedValue({ data: menuPayload });

    renderOrderPage({ pathname: "/order", state: {} });

    await waitFor(() => {
      expect(axios.get).toHaveBeenCalledWith("/menu");
    });

    expect(document.querySelectorAll(".order-item")).toHaveLength(0);
  });
});
