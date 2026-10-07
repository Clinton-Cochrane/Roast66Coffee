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

vi.mock("./OrderPage", () => ({
  default: function MockOrderPage() {
    return <div>Mock Order Builder</div>;
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

  it("defaults to New Order and switches views without navigating", () => {
    localStorage.setItem("token", "x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
    render(
      <MemoryRouter>
        <CashPage />
      </MemoryRouter>
    );
    const newOrderTab = screen.getByRole("tab", { name: "New Order" });
    const ordersTab = screen.getByRole("tab", { name: "Orders" });
    expect(newOrderTab).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText("Mock Order Builder")).toBeVisible();
    expect(screen.getByText("Mock Orders")).not.toBeVisible();
    expect(screen.queryByRole("button", { name: "New Order" })).not.toBeInTheDocument();

    fireEvent.click(ordersTab);
    expect(ordersTab).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tabpanel", { name: "Orders" })).toHaveTextContent("Mock Orders");
    expect(screen.getByText("Mock Order Builder")).not.toBeVisible();

    fireEvent.click(newOrderTab);
    expect(screen.getByRole("tabpanel", { name: "New Order" })).toHaveTextContent("Mock Order Builder");
    expect(mockNavigate).not.toHaveBeenCalled();
  });

  it("supports arrow, Home, and End keys for tab selection and focus", () => {
    localStorage.setItem("token", "x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
    render(<MemoryRouter><CashPage /></MemoryRouter>);
    const newOrderTab = screen.getByRole("tab", { name: "New Order" });
    const ordersTab = screen.getByRole("tab", { name: "Orders" });

    newOrderTab.focus();
    fireEvent.keyDown(newOrderTab, { key: "ArrowRight" });
    expect(ordersTab).toHaveFocus();
    expect(ordersTab).toHaveAttribute("aria-selected", "true");
    expect(newOrderTab).toHaveAttribute("tabindex", "-1");

    fireEvent.keyDown(ordersTab, { key: "ArrowRight" });
    expect(newOrderTab).toHaveFocus();
    fireEvent.keyDown(newOrderTab, { key: "ArrowLeft" });
    expect(ordersTab).toHaveFocus();
    fireEvent.keyDown(ordersTab, { key: "Home" });
    expect(newOrderTab).toHaveFocus();
    fireEvent.keyDown(newOrderTab, { key: "End" });
    expect(ordersTab).toHaveFocus();
  });

  it("logs out and routes back to /cash", () => {
    localStorage.setItem("token", "x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
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
