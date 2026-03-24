import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import AdminPage from "./AdminPage";

jest.mock("../components/Admin/ViewOrders", () => function MockViewOrders() {
  return <div data-testid="mock-view-orders">Orders content</div>;
});

jest.mock("../components/Admin/ManageMenu", () => function MockManageMenu() {
  return <div data-testid="mock-manage-menu">Menu content</div>;
});

jest.mock("../components/Admin/MenuBulkOperations", () => function MockMenuBulk() {
  return <div data-testid="mock-menu-bulk">Bulk content</div>;
});

jest.mock("../components/Admin/NotificationSettings", () => function MockNotif() {
  return <div data-testid="mock-notification-settings">Settings content</div>;
});

jest.mock("../components/layout/Header", () => function MockHeader({ title }) {
  return <header data-testid="admin-header">{title}</header>;
});

jest.mock("../components/common/Loading", () => function MockLoading() {
  return <div data-testid="loading">Loading</div>;
});

function renderAdminPage() {
  return render(
    <MemoryRouter>
      <AdminPage />
    </MemoryRouter>,
  );
}

describe("AdminPage", () => {
  beforeEach(() => {
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
});
