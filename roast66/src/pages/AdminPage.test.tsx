import React from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import AdminPage from "./AdminPage";

const mockNavigate = vi.fn();

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock("../hooks/useKeepAliveHeartbeat", () => ({
  default: vi.fn(),
}));

vi.mock("../components/Admin/ViewOrders", () => ({
  default: function MockViewOrders() {
    return <div data-testid="mock-view-orders">Orders content</div>;
  },
}));

vi.mock("../components/Admin/ManageMenu", () => ({
  default: function MockManageMenu() {
    return <div data-testid="mock-manage-menu">Menu content</div>;
  },
}));

vi.mock("../components/Admin/MenuBulkOperations", () => ({
  default: function MockMenuBulk() {
    return <div data-testid="mock-menu-bulk">Bulk content</div>;
  },
}));

vi.mock("../components/Admin/NotificationSettings", () => ({
  default: function MockNotif() {
    return <div data-testid="mock-notification-settings">Settings content</div>;
  },
}));

vi.mock("../components/layout/Header", () => ({
  default: function MockHeader({ title }: { title: string }) {
    return <header data-testid="admin-header">{title}</header>;
  },
}));

vi.mock("../components/common/Loading", () => ({
  default: function MockLoading() {
    return <div data-testid="loading">Loading</div>;
  },
}));

function renderAdminPage() {
  return render(
    <MemoryRouter>
      <AdminPage />
    </MemoryRouter>
  );
}

describe("AdminPage", () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    localStorage.setItem("token", "test-token");
  });

  afterEach(() => {
    localStorage.removeItem("token");
  });

  it("shows Orders tab by default", () => {
    renderAdminPage();
    expect(screen.getByTestId("mock-view-orders")).toBeInTheDocument();
    expect(screen.queryByTestId("mock-manage-menu")).not.toBeInTheDocument();
    expect(screen.queryByTestId("mock-notification-settings")).not.toBeInTheDocument();
  });

  it("shows menu management when Menu tab is selected", () => {
    renderAdminPage();
    fireEvent.click(screen.getByRole("tab", { name: /menu management/i }));
    expect(screen.getByTestId("mock-menu-bulk")).toBeInTheDocument();
    expect(screen.getByTestId("mock-manage-menu")).toBeInTheDocument();
    expect(screen.queryByTestId("mock-view-orders")).not.toBeInTheDocument();
  });

  it("shows settings when Settings tab is selected", () => {
    renderAdminPage();
    fireEvent.click(screen.getByRole("tab", { name: /^settings$/i }));
    expect(screen.getByTestId("mock-notification-settings")).toBeInTheDocument();
    expect(screen.queryByTestId("mock-view-orders")).not.toBeInTheDocument();
  });

  it("logs out and routes back to /admin", () => {
    renderAdminPage();
    fireEvent.click(screen.getByRole("button", { name: /log out/i }));
    expect(localStorage.getItem("token")).toBeNull();
    expect(mockNavigate).toHaveBeenCalledWith("/admin", { replace: true });
  });
});
