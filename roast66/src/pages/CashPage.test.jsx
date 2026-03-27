import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import CashPage from "./CashPage";

const mockNavigate = jest.fn();
jest.mock("react-router-dom", () => ({
  ...jest.requireActual("react-router-dom"),
  useNavigate: () => mockNavigate,
}));

jest.mock("../components/layout/Header", () => function MockHeader({ title }) {
  return <header>{title}</header>;
});

jest.mock("../components/Admin/ViewOrders", () => function MockViewOrders() {
  return <div>Mock Orders</div>;
});

describe("CashPage", () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    localStorage.clear();
  });

  it("navigates to /cash on mount when token is missing", () => {
    render(
      <MemoryRouter>
        <CashPage />
      </MemoryRouter>,
    );
    expect(mockNavigate).toHaveBeenCalledWith("/cash", { replace: true });
  });

  it("navigates to /order from New Order button", () => {
    localStorage.setItem("token", "jwt-token");
    render(
      <MemoryRouter>
        <CashPage />
      </MemoryRouter>,
    );
    fireEvent.click(screen.getByRole("button", { name: /new order/i }));
    expect(mockNavigate).toHaveBeenCalledWith("/order");
  });

  it("logs out and routes back to /cash", () => {
    localStorage.setItem("token", "jwt-token");
    render(
      <MemoryRouter>
        <CashPage />
      </MemoryRouter>,
    );
    fireEvent.click(screen.getByRole("button", { name: /log out/i }));
    expect(localStorage.getItem("token")).toBeNull();
    expect(mockNavigate).toHaveBeenCalledWith("/cash", { replace: true });
  });
});
