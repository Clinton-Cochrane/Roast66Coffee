import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import AdminGate from "./AdminGate";

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

vi.mock("../../pages/AdminPage", () => ({
  default: function MockAdminPage() {
    return <div>Mock Admin Page</div>;
  },
}));

describe("AdminGate", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("shows admin page when token exists", () => {
    localStorage.setItem("token", "jwt-token");
    render(<AdminGate />);
    expect(screen.getByText("Mock Admin Page")).toBeInTheDocument();
  });

  it("shows login first, then admin page after login success", () => {
    render(<AdminGate />);
    localStorage.setItem("token", "jwt-token");
    fireEvent.click(screen.getByRole("button", { name: /mock admin login/i }));
    expect(screen.getByText("Mock Admin Page")).toBeInTheDocument();
  });
});
