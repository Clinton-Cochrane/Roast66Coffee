import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
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

const filterableMenuPayload = [
  {
    id: 10,
    name: "Blue Flame Nitro",
    price: 5.25,
    description: "Nitro cold brew",
    categoryType: CategoryType.SPECIALS,
    isFeaturedOnHome: false,
  },
  {
    id: 11,
    name: "Latte",
    price: 3.5,
    description: "Espresso with steamed milk",
    categoryType: CategoryType.COFFEE,
    isFeaturedOnHome: false,
  },
  {
    id: 12,
    name: "Green Nova Refresher",
    price: 3.75,
    description: "A sparkling lime drink",
    categoryType: CategoryType.DRINKS,
    isFeaturedOnHome: false,
  },
  {
    id: 13,
    name: "Featured Mocha",
    price: 4,
    description: "Featured chocolate coffee",
    categoryType: CategoryType.COFFEE,
    isFeaturedOnHome: true,
  },
  {
    id: 14,
    name: "Pink Slip Punch",
    price: 3.5,
    description: "Strawberry and lemon punch",
    categoryType: CategoryType.SPECIALS,
    isFeaturedOnHome: false,
  },
  {
    id: 15,
    name: "Vanilla",
    price: 0.5,
    description: "Sweet flavor",
    categoryType: CategoryType.FLAVORS,
    isFeaturedOnHome: false,
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

function mockMobileOrderLayout(matches: boolean) {
  const mediaQueryList = {
    matches,
    media: "(max-width: 960px)",
    onchange: null,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    dispatchEvent: vi.fn(),
  } as unknown as MediaQueryList;

  Object.defineProperty(window, "matchMedia", {
    configurable: true,
    writable: true,
    value: vi.fn(() => mediaQueryList),
  });
}

async function buildBasicOrder() {
  fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
  fireEvent.click(await screen.findByRole("button", { name: "Order Espresso" }));
  fireEvent.change(screen.getByRole("textbox", { name: "Your Name" }), {
    target: { value: "Ada Lovelace" },
  });
}

describe("OrderPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockMobileOrderLayout(false);
  });

  it("prefills cart from navigation state menuItemId after menu loads", async () => {
    mockGet.mockResolvedValue({ data: menuPayload });

    renderOrderPage({ pathname: "/order", state: { menuItemId: 1 } });

    await waitFor(() => {
      expect(screen.getAllByTestId("order-item")).toHaveLength(1);
    });
    expect(screen.getByTestId("order-item")).toHaveTextContent("Espresso");

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

    expect(screen.queryAllByTestId("order-item")).toHaveLength(0);
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

    expect(screen.queryAllByTestId("order-item")).toHaveLength(0);
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

    expect(screen.queryAllByTestId("order-item")).toHaveLength(0);
  });

  it("renders directly orderable menu items as compact action rows", async () => {
    mockGet.mockResolvedValue({ data: menuPayloadWithFlavor });

    renderOrderPage({ pathname: "/order", state: {} });

    fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
    expect(await screen.findByRole("button", { name: "Order Espresso" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Order Vanilla" })).not.toBeInTheDocument();
    expect(
      screen.queryByRole("combobox", { name: "Select a menu item" })
    ).not.toBeInTheDocument();
  });

  it("marks a drink row while any instance of that drink is in the order", async () => {
    mockGet.mockResolvedValue({ data: menuPayload });

    renderOrderPage({ pathname: "/order", state: {} });

    fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
    const drinkRow = await screen.findByRole("button", { name: "Order Espresso" });
    expect(drinkRow).not.toHaveAttribute("data-in-order");

    fireEvent.click(drinkRow);

    const selectedDrinkRow = screen.getByRole("button", {
      name: "Add another Espresso; currently in order",
    });
    expect(selectedDrinkRow).toHaveAttribute("data-in-order", "true");

    fireEvent.click(selectedDrinkRow);
    expect(screen.getAllByTestId("order-item")).toHaveLength(2);

    fireEvent.click(screen.getAllByRole("button", { name: "Remove item" })[0]);
    expect(
      screen.getByRole("button", { name: "Add another Espresso; currently in order" })
    ).toHaveAttribute("data-in-order", "true");

    fireEvent.click(screen.getByRole("button", { name: "Remove item" }));
    expect(screen.getByRole("button", { name: "Order Espresso" })).not.toHaveAttribute(
      "data-in-order"
    );
  });

  it("shows one active customizer, defaults to the last added item, and switches editors", async () => {
    mockGet.mockResolvedValue({ data: filterableMenuPayload });

    renderOrderPage({ pathname: "/order", state: {} });

    fireEvent.click(screen.getByRole("button", { name: "All" }));
    fireEvent.click(
      await screen.findByRole("button", { name: "Order Blue Flame Nitro" })
    );

    expect(
      screen.getByRole("heading", { name: "Customize Blue Flame Nitro" })
    ).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Order Latte" }));

    expect(screen.getAllByTestId("order-item")).toHaveLength(2);
    expect(screen.getByRole("heading", { name: "Customize Latte" })).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Customize Blue Flame Nitro" })
    ).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Edit Latte" })).toHaveAttribute(
      "aria-pressed",
      "true"
    );
    expect(
      screen.getByRole("button", { name: "Edit Blue Flame Nitro" })
    ).toHaveAttribute("aria-pressed", "false");

    fireEvent.click(screen.getByRole("button", { name: "Edit Blue Flame Nitro" }));

    expect(
      screen.getByRole("heading", { name: "Customize Blue Flame Nitro" })
    ).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Customize Latte" })).not.toBeInTheDocument();
    expect(
      screen.getByRole("spinbutton", { name: "Quantity for Blue Flame Nitro" })
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("spinbutton", { name: "Quantity for Latte" })
    ).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Remove Blue Flame Nitro" }));

    expect(screen.getAllByTestId("order-item")).toHaveLength(1);
    expect(screen.getByRole("heading", { name: "Customize Latte" })).toBeInTheDocument();
  });

  it("preserves customization totals and the existing order submission payload", async () => {
    mockGet.mockResolvedValue({ data: menuPayloadWithFlavor });
    mockPost.mockResolvedValue({ data: { id: 44, orderItems: [] } });

    renderOrderPage({ pathname: "/order", state: {} });

    fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
    fireEvent.click(await screen.findByRole("button", { name: "Order Espresso" }));

    fireEvent.change(screen.getByRole("spinbutton", { name: "Quantity for Espresso" }), {
      target: { value: "2" },
    });
    fireEvent.change(screen.getByRole("combobox", { name: "Add a Flavor" }), {
      target: { value: JSON.stringify(menuPayloadWithFlavor[1]) },
    });
    fireEvent.change(screen.getByRole("textbox", { name: "Notes (optional)" }), {
      target: { value: "Light ice" },
    });
    fireEvent.change(screen.getByRole("textbox", { name: "Your Name" }), {
      target: { value: "Alex" },
    });

    expect(screen.getByRole("heading", { name: "Current Order" })).toBeInTheDocument();
    expect(screen.getAllByText("$5.50").length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole("button", { name: "Place Order" }));

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith("/admin/orders", {
        customerName: "Alex",
        customerPhone: null,
        customerEmail: null,
        customerNotificationOptIn: false,
        orderItems: [
          {
            menuItemId: 1,
            quantity: 2,
            notes: "Light ice",
            addOns: [{ menuItemId: 2, quantity: 1 }],
          },
        ],
      });
    });
    expect(mockNavigate).toHaveBeenCalledWith("/order/confirmation", {
      state: { order: { id: 44, orderItems: [] } },
    });
  });

  it("shows submission progress and prevents duplicate requests", async () => {
    mockGet.mockResolvedValue({ data: menuPayload });

    let resolvePost: ((value: { data: { id: number } }) => void) | undefined;
    mockPost.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolvePost = resolve;
        })
    );

    renderOrderPage({ pathname: "/order", state: {} });
    await buildBasicOrder();

    const placeOrderButton = screen.getByRole("button", { name: "Place Order" });
    const form = placeOrderButton.closest("form");
    expect(form).not.toBeNull();

    fireEvent.click(placeOrderButton);

    const submittingButton = await screen.findByRole("button", { name: "Placing order…" });
    expect(submittingButton).toBeDisabled();
    expect(mockPost).toHaveBeenCalledTimes(1);

    fireEvent.submit(form as HTMLFormElement);
    expect(mockPost).toHaveBeenCalledTimes(1);

    resolvePost?.({ data: { id: 42 } });

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith("/order/confirmation", {
        state: { order: { id: 42 } },
      });
    });
  });

  it("restores the current-order submit button after a failed request", async () => {
    mockGet.mockResolvedValue({ data: menuPayload });
    mockPost.mockRejectedValue(new Error("Network unavailable"));

    renderOrderPage({ pathname: "/order", state: {} });
    await buildBasicOrder();

    fireEvent.click(screen.getByRole("button", { name: "Place Order" }));

    await waitFor(() => {
      expect(toastFns.error).toHaveBeenCalledWith(
        "Failed to place the order. Please try again or check the console for details."
      );
    });

    expect(screen.getByRole("button", { name: "Place Order" })).toBeEnabled();
  });

  it("keeps submission feedback visible until a duplicate order is identified", async () => {
    mockGet.mockResolvedValue({ data: menuPayload });

    let rejectPost: ((reason: unknown) => void) | undefined;
    mockPost.mockImplementation(
      () =>
        new Promise((_, reject) => {
          rejectPost = reject;
        })
    );

    renderOrderPage({ pathname: "/order", state: {} });
    await buildBasicOrder();

    fireEvent.click(screen.getByRole("button", { name: "Place Order" }));

    expect(await screen.findByRole("button", { name: "Placing order…" })).toBeDisabled();

    rejectPost?.({
      isAxiosError: true,
      response: {
        status: 409,
        data: { order: { id: 42 }, existingOrderId: 42 },
      },
    });

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith("/order/duplicate", {
        state: { order: { id: 42 }, existingOrderId: 42 },
      });
    });
  });

  it("caps drink quantities at 12 and removes a line when its quantity becomes zero", async () => {
    mockGet.mockResolvedValue({ data: menuPayload });

    renderOrderPage({ pathname: "/order", state: {} });

    fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
    fireEvent.click(await screen.findByRole("button", { name: "Order Espresso" }));

    const quantityInput = screen.getByRole("spinbutton", {
      name: "Quantity for Espresso",
    });
    expect(quantityInput).toHaveAttribute("min", "0");
    expect(quantityInput).toHaveAttribute("max", "12");
    expect(quantityInput).toHaveAttribute("step", "1");
    expect(quantityInput).toHaveAttribute("inputmode", "numeric");
    expect(quantityInput).toHaveAccessibleDescription(
      "Enter 1 through 12. Enter 0 to remove it from the order."
    );

    fireEvent.change(quantityInput, { target: { value: "99" } });

    expect(quantityInput).toHaveValue(12);
    expect(screen.getAllByText("$30.00").length).toBeGreaterThan(0);

    fireEvent.change(quantityInput, { target: { value: "0" } });

    expect(screen.queryByTestId("order-item")).not.toBeInTheDocument();
    expect(screen.getByText("Your order is empty.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Place Order" })).toBeDisabled();
  });

  it("caps each flavor at 12 and removes only the flavor when its quantity becomes zero", async () => {
    const chocolateFlavor = {
      id: 3,
      name: "Chocolate",
      price: 0.75,
      description: "Chocolate flavor",
      categoryType: CategoryType.FLAVORS,
    };
    mockGet.mockResolvedValue({
      data: [...menuPayloadWithFlavor, chocolateFlavor],
    });

    renderOrderPage({ pathname: "/order", state: {} });

    fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
    fireEvent.click(await screen.findByRole("button", { name: "Order Espresso" }));

    const flavorSelect = screen.getByRole("combobox", { name: "Add a Flavor" });
    fireEvent.change(flavorSelect, {
      target: { value: JSON.stringify(menuPayloadWithFlavor[1]) },
    });
    fireEvent.change(flavorSelect, {
      target: { value: JSON.stringify(chocolateFlavor) },
    });

    const vanillaQuantity = screen.getByRole("spinbutton", {
      name: "Quantity for Vanilla",
    });
    expect(vanillaQuantity).toHaveAttribute("min", "0");
    expect(vanillaQuantity).toHaveAttribute("max", "12");
    expect(vanillaQuantity).toHaveAttribute("step", "1");
    expect(vanillaQuantity).toHaveAttribute("inputmode", "numeric");
    expect(vanillaQuantity).toHaveAccessibleDescription(
      "Enter 1 through 12. Enter 0 to remove it from the order."
    );
    expect(
      screen.getByRole("spinbutton", { name: "Quantity for Chocolate" })
    ).toBeInTheDocument();

    fireEvent.change(vanillaQuantity, { target: { value: "99" } });

    expect(vanillaQuantity).toHaveValue(12);
    expect(screen.getAllByText("$9.25").length).toBeGreaterThan(0);

    fireEvent.change(vanillaQuantity, { target: { value: "0" } });

    expect(
      screen.queryByRole("spinbutton", { name: "Quantity for Vanilla" })
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("spinbutton", { name: "Quantity for Chocolate" })
    ).toBeInTheDocument();
    expect(screen.getByRole("spinbutton", { name: "Quantity for Espresso" })).toHaveValue(1);
    expect(screen.getAllByText("$3.25").length).toBeGreaterThan(0);
  });

  it("defaults to Daily Specials and exposes the existing orderable categories", async () => {
    mockGet.mockResolvedValue({ data: filterableMenuPayload });

    renderOrderPage({ pathname: "/order", state: {} });

    const dailySpecials = screen.getByRole("button", { name: "Daily Specials" });
    expect(dailySpecials).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Coffee" })).toHaveAttribute(
      "aria-pressed",
      "false"
    );
    expect(screen.getByRole("button", { name: "Drinks" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "All" })).toBeInTheDocument();

    expect(await screen.findByRole("button", { name: "Order Blue Flame Nitro" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Order Featured Mocha" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Order Pink Slip Punch" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Order Latte" })).not.toBeInTheDocument();
  });

  it("searches within the selected category and keeps All in API order", async () => {
    mockGet.mockResolvedValue({ data: filterableMenuPayload });

    renderOrderPage({ pathname: "/order", state: {} });

    fireEvent.click(screen.getByRole("button", { name: "All" }));
    const drinkList = await screen.findByRole("list", { name: "Available drinks" });
    expect(
      within(drinkList)
        .getAllByRole("button")
        .map((button) => button.getAttribute("aria-label"))
    ).toEqual([
      "Order Blue Flame Nitro",
      "Order Latte",
      "Order Green Nova Refresher",
      "Order Featured Mocha",
      "Order Pink Slip Punch",
    ]);

    fireEvent.change(screen.getByRole("searchbox", { name: "Search drinks" }), {
      target: { value: "sparkling" },
    });

    expect(screen.getByRole("button", { name: "Order Green Nova Refresher" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Order Blue Flame Nitro" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
    expect(screen.getByRole("status")).toHaveTextContent(
      "No drinks match this search and category."
    );
  });

  it("opens the live customizer as an accessible mobile sheet and restores its trigger", async () => {
    mockMobileOrderLayout(true);
    mockGet.mockResolvedValue({ data: menuPayloadWithFlavor });

    renderOrderPage({ pathname: "/order", state: {} });

    fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
    const drinkButton = await screen.findByRole("button", { name: "Order Espresso" });
    fireEvent.click(drinkButton);

    const dialog = await screen.findByRole("dialog", { name: "Order details" });
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveTextContent("Customize Espresso");
    expect(document.body.style.overflow).toBe("hidden");
    await waitFor(() => {
      expect(screen.getByRole("button", { name: "Close order details" })).toHaveFocus();
    });

    fireEvent.keyDown(document, { key: "Escape" });

    expect(screen.queryByRole("dialog", { name: "Order details" })).not.toBeInTheDocument();
    expect(drinkButton).toHaveFocus();
    await waitFor(() => {
      expect(document.body.style.overflow).toBe("");
    });
  });

  it("keeps the sticky mobile summary synchronized and reopens the order sheet", async () => {
    mockMobileOrderLayout(true);
    mockGet.mockResolvedValue({ data: menuPayloadWithFlavor });

    renderOrderPage({ pathname: "/order", state: {} });

    const summaryBar = screen.getByRole("button", { name: /Current Order.*0 items.*\$0\.00/ });
    expect(summaryBar).toHaveAttribute("aria-expanded", "false");

    fireEvent.click(screen.getByRole("button", { name: "Coffee" }));
    fireEvent.click(await screen.findByRole("button", { name: "Order Espresso" }));
    fireEvent.change(screen.getByRole("spinbutton", { name: "Quantity for Espresso" }), {
      target: { value: "2" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Close order details" }));

    const updatedSummaryBar = screen.getByRole("button", {
      name: /Current Order.*2 items.*\$5\.00/,
    });
    expect(updatedSummaryBar).toHaveAttribute("aria-expanded", "false");

    fireEvent.click(updatedSummaryBar);

    expect(await screen.findByRole("dialog", { name: "Order details" })).toBeInTheDocument();
    expect(updatedSummaryBar).toHaveAttribute("aria-expanded", "true");
  });
});
