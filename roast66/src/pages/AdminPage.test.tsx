import React from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import AdminPage from "./AdminPage";
import { LanguageProvider } from "../i18n/LanguageContext";

const mockNavigate = vi.fn();
const mockGet = vi.fn();

vi.mock("../axiosConfig", () => ({
  default: {
    get: (...args: unknown[]) => mockGet(...args),
  },
}));

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

vi.mock("../components/Admin/StaffManagement", () => ({
  default: function MockStaffManagement() {
    return <div data-testid="mock-staff-management">Staff content</div>;
  },
}));

vi.mock("../components/Admin/AccountSecurity", () => ({
  default: function MockAccountSecurity() {
    return <div data-testid="mock-account-security">Account content</div>;
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
    mockGet.mockReset();
    mockGet.mockResolvedValue({
      data: { id: "admin-1", displayName: "Admin", username: "admin", isActive: true, roles: ["Admin"] },
    });
    localStorage.setItem("token", "x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
  });

  afterEach(() => {
    localStorage.removeItem("token");
    localStorage.removeItem("roast66_locale");
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

  it("shows staff management only for an Owner", async () => {
    mockGet.mockResolvedValueOnce({
      data: { id: "owner-1", displayName: "Owner", username: "owner", isActive: true, roles: ["Admin", "Owner"] },
    });
    renderAdminPage();
    fireEvent.click(await screen.findByRole("tab", { name: /^staff$/i }));
    expect(screen.getByTestId("mock-staff-management")).toBeInTheDocument();
  });

  it("shows account security for a normal Admin", async () => {
    renderAdminPage();
    fireEvent.click(await screen.findByRole("tab", { name: /^account$/i }));
    expect(screen.getByTestId("mock-account-security")).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: /^staff$/i })).not.toBeInTheDocument();
  });

  it("logs out and routes back to /admin", () => {
    renderAdminPage();
    fireEvent.click(screen.getByRole("button", { name: /log out/i }));
    expect(localStorage.getItem("token")).toBeNull();
    expect(mockNavigate).toHaveBeenCalledWith("/admin", { replace: true });
  });
});

describe("AdminPage (es-MX locale)", () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    mockGet.mockReset();
    mockGet.mockResolvedValue({
      data: { id: "admin-1", displayName: "Admin", username: "admin", isActive: true, roles: ["Admin"] },
    });
    localStorage.setItem("token", "x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
    localStorage.setItem("roast66_locale", "es");
  });

  afterEach(() => {
    localStorage.removeItem("token");
    localStorage.removeItem("roast66_locale");
  });

  function renderAdminPageEs() {
    return render(
      <MemoryRouter>
        <LanguageProvider>
          <AdminPage />
        </LanguageProvider>
      </MemoryRouter>
    );
  }

  it("renders Spanish tab labels and logout", () => {
    renderAdminPageEs();
    expect(screen.getByRole("tab", { name: /^pedidos$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /cerrar sesión/i })).toBeInTheDocument();
    expect(screen.getByTestId("admin-header")).toHaveTextContent("Panel de administración");
  });

  it("switches to menu tab using Spanish label", () => {
    renderAdminPageEs();
    fireEvent.click(screen.getByRole("tab", { name: /gestión del menú/i }));
    expect(screen.getByTestId("mock-menu-bulk")).toBeInTheDocument();
  });
});
