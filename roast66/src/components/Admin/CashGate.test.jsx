import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import CashGate from "./CashGate";

jest.mock("../../pages/AdminLogin", () => function MockAdminLogin({ onLoginSuccess }) {
  return (
    <button type="button" onClick={onLoginSuccess}>
      Mock Admin Login
    </button>
  );
});

jest.mock("../../pages/CashPage", () => function MockCashPage() {
  return <div>Mock Cash Page</div>;
});

describe("CashGate", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("shows cash page when token exists", () => {
    localStorage.setItem("token", "jwt-token");
    render(<CashGate />);
    expect(screen.getByText("Mock Cash Page")).toBeInTheDocument();
  });

  it("shows login first, then cash page after login success", () => {
    render(<CashGate />);
    localStorage.setItem("token", "jwt-token");
    fireEvent.click(screen.getByRole("button", { name: /mock admin login/i }));
    expect(screen.getByText("Mock Cash Page")).toBeInTheDocument();
  });
});
