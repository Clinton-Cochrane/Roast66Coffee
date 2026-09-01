import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AccountSecurity from "./AccountSecurity";

const mockPost = vi.fn();
const mockSuccess = vi.fn();

vi.mock("../../axiosConfig", () => ({
  default: { post: (...args: unknown[]) => mockPost(...args) },
}));

vi.mock("react-toastify", () => ({
  toast: { success: (...args: unknown[]) => mockSuccess(...args), error: vi.fn() },
}));

describe("AccountSecurity", () => {
  beforeEach(() => {
    mockPost.mockReset();
    mockSuccess.mockReset();
    mockPost.mockResolvedValue({});
  });

  it("changes only the signed-in account password", async () => {
    render(
      <AccountSecurity
        account={{
          id: "staff-1",
          displayName: "Mary",
          username: "mary",
          isActive: true,
          roles: ["Admin"],
        }}
      />
    );

    fireEvent.change(screen.getByLabelText(/current password/i), {
      target: { value: "OldPassword1!" },
    });
    fireEvent.change(screen.getByLabelText(/new password/i), {
      target: { value: "NewPassword2!" },
    });
    fireEvent.click(screen.getByRole("button", { name: /change password/i }));

    await waitFor(() =>
      expect(mockPost).toHaveBeenCalledWith("/admin/me/change-password", {
        currentPassword: "OldPassword1!",
        newPassword: "NewPassword2!",
      })
    );
    expect(mockSuccess).toHaveBeenCalled();
  });
});
