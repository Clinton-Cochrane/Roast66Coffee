import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import CashGate from "./CashGate";

vi.mock("../../pages/AdminLogin", () => ({
  default: function MockAdminLogin({
    onLoginSuccess,
  }: {
    onLoginSuccess?: () => void;
  }) {
    return (
      <button type="button" onClick={onLoginSuccess}>
        Mock Admin Login
      </button>
    );
  },
}));

vi.mock("../../pages/CashPage", () => ({
  default: function MockCashPage() {
    return <div>Mock Cash Page</div>;
  },
}));

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
