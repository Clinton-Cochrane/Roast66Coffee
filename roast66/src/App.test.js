import React from "react";
import { render, screen } from "@testing-library/react";
import App from "./App";

// Mock child components to simplify testing
jest.mock("./pages/HomePage", () => () => <div>Home Page</div>);
jest.mock("./pages/MenuPage", () => () => <div>Menu Page</div>);
jest.mock("./pages/OrderPage", () => () => <div>Order Page</div>);
jest.mock("./pages/OrderConfirmationPage", () => () => <div>Order Confirmation</div>);
jest.mock("./pages/OrderStatusPage", () => () => <div>Order Status</div>);
jest.mock("./pages/AdminPage", () => () => <div>Admin Page</div>);
jest.mock("./pages/AdminLogin", () => () => <div>Admin Login</div>);
jest.mock("./components/Navigation", () => () => <nav>Navigation</nav>);
jest.mock("./components/layout/Footer", () => () => <footer>Footer</footer>);
jest.mock("./components/Admin/PrivateRoute", () => ({ children }) => <div>{children}</div>);
jest.mock("react-toastify", () => ({
  ToastContainer: () => null,
  toast: {},
}));

// Use MemoryRouter instead of BrowserRouter for tests (App uses BrowserRouter)
jest.mock("react-router-dom", () => ({
  ...jest.requireActual("react-router-dom"),
  BrowserRouter: ({ children }) => {
    const { MemoryRouter } = jest.requireActual("react-router-dom");
    return <MemoryRouter initialEntries={["/"]}>{children}</MemoryRouter>;
  },
}));

describe("App", () => {
  it("renders navigation and home page", () => {
    render(<App />);
    expect(screen.getByRole("navigation")).toBeInTheDocument();
    expect(screen.getByText("Home Page")).toBeInTheDocument();
  });
});
