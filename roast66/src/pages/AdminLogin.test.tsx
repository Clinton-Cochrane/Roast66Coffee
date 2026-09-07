import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import AdminLogin from "./AdminLogin";
import { LanguageProvider } from "../i18n/LanguageContext";

const mockPost = vi.fn();

vi.mock("../axiosConfig", () => ({
  default: {
    post: (...args: unknown[]) => mockPost(...args),
  },
}));

function renderLogin() {
  return render(
    <LanguageProvider>
      <AdminLogin />
    </LanguageProvider>
  );
}

describe("AdminLogin", () => {
  const mockOnLoginSuccess = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("renders login form", () => {
    renderLogin();
    expect(screen.getByRole("heading", { name: /admin login/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Username")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Password")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /login/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /forgot password/i })).not.toBeInTheDocument();
  });

  it("calls onLoginSuccess and stores token on successful login", async () => {
    mockPost.mockResolvedValueOnce({ data: { token: "jwt-token" } });
    render(
      <LanguageProvider>
        <AdminLogin onLoginSuccess={mockOnLoginSuccess} />
      </LanguageProvider>
    );

    fireEvent.change(screen.getByPlaceholderText("Username"), {
      target: { value: "admin" },
    });
    fireEvent.change(screen.getByPlaceholderText("Password"), {
      target: { value: "password" },
    });
    fireEvent.click(screen.getByRole("button", { name: /login/i }));

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith("/admin/login", {
        username: "admin",
        password: "password",
      });
    });
    expect(localStorage.getItem("token")).toBe("jwt-token");
    await waitFor(() => {
      expect(mockOnLoginSuccess).toHaveBeenCalled();
    });
  });

  it("shows error message on failed login", async () => {
    mockPost.mockRejectedValueOnce({
      isAxiosError: true,
      response: { status: 401 },
    });
    renderLogin();

    fireEvent.change(screen.getByPlaceholderText("Username"), {
      target: { value: "wrong" },
    });
    fireEvent.change(screen.getByPlaceholderText("Password"), {
      target: { value: "wrong" },
    });
    fireEvent.click(screen.getByRole("button", { name: /login/i }));

    await waitFor(() => {
      expect(screen.getByText("Invalid credentials")).toBeInTheDocument();
    });
  });
});
