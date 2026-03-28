import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import App from "./App";

vi.mock("./pages/HomePage", () => ({
  default: () => <div>Home Page</div>,
}));
vi.mock("./pages/MenuPage", () => ({
  default: () => <div>Menu Page</div>,
}));
vi.mock("./pages/OrderPage", () => ({
  default: () => <div>Order Page</div>,
}));
vi.mock("./pages/OrderConfirmationPage", () => ({
  default: () => <div>Order Confirmation</div>,
}));
vi.mock("./pages/OrderStatusPage", () => ({
  default: () => <div>Order Status</div>,
}));
vi.mock("./components/Admin/AdminGate", () => ({
  default: () => <div>Admin Gate</div>,
}));
vi.mock("./components/Admin/CashGate", () => ({
  default: () => <div>Cash Gate</div>,
}));
vi.mock("./components/Navigation", () => ({
  default: () => <nav>Navigation</nav>,
}));
vi.mock("./components/layout/Footer", () => ({
  default: () => <footer>Footer</footer>,
}));
vi.mock("react-toastify", () => ({
  ToastContainer: () => null,
  toast: {},
}));

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    BrowserRouter: ({ children }: { children: React.ReactNode }) => (
      <actual.MemoryRouter initialEntries={["/"]}>{children}</actual.MemoryRouter>
    ),
  };
});

describe("App", () => {
  it("renders navigation and home page", async () => {
    render(<App />);
    expect(screen.getByRole("navigation")).toBeInTheDocument();
    expect(await screen.findByText("Home Page")).toBeInTheDocument();
  });
});
