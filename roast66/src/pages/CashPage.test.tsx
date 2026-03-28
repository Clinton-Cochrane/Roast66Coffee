import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import CashPage from "./CashPage";

const mockNavigate = vi.fn();

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock("../components/layout/Header", () => ({
  default: function MockHeader({ title }: { title: string }) {
    return <header>{title}</header>;
  },
}));

vi.mock("../components/Admin/ViewOrders", () => ({
  default: function MockViewOrders() {
    return <div>Mock Orders</div>;
  },
}));

vi.mock("../components/Admin/StaffDevicePrompt", () => ({
  default: function MockStaffPrompt() {
    return null;
  },
}));

describe("CashPage", () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    localStorage.clear();
  });

  it("navigates to /cash on mount when token is missing", () => {
    render(
      <MemoryRouter>
        <CashPage />
      </MemoryRouter>
    );
    expect(mockNavigate).toHaveBeenCalledWith("/cash", { replace: true });
  });

  it("navigates to /order from New Order button", () => {
    localStorage.setItem("token", "jwt-token");
    render(
      <MemoryRouter>
        <CashPage />
      </MemoryRouter>
    );
    fireEvent.click(screen.getByRole("button", { name: /new order/i }));
    expect(mockNavigate).toHaveBeenCalledWith("/order");
  });

  it("logs out and routes back to /cash", () => {
    localStorage.setItem("token", "jwt-token");
    render(
      <MemoryRouter>
        <CashPage />
      </MemoryRouter>
    );
    fireEvent.click(screen.getByRole("button", { name: /log out/i }));
    expect(localStorage.getItem("token")).toBeNull();
    expect(mockNavigate).toHaveBeenCalledWith("/cash", { replace: true });
  });
});
