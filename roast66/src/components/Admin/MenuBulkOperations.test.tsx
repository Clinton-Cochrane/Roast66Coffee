import React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { toast } from "react-toastify";
import MenuBulkOperations from "./MenuBulkOperations";
import { LanguageProvider } from "../../i18n/LanguageContext";

const mockGet = vi.fn();
const mockPost = vi.fn();

vi.mock("../../axiosConfig", () => ({
  default: {
    get: (...args: unknown[]) => mockGet(...args),
    post: (...args: unknown[]) => mockPost(...args),
  },
}));

vi.mock("react-toastify", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

function renderOperations(onMenuUpdated = vi.fn()) {
  render(
    <LanguageProvider>
      <MenuBulkOperations onMenuUpdated={onMenuUpdated} />
    </LanguageProvider>
  );
  return onMenuUpdated;
}

describe("MenuBulkOperations default-menu reset", () => {
  beforeEach(() => {
    mockGet.mockReset();
    mockPost.mockReset();
    vi.mocked(toast.success).mockReset();
    vi.mocked(toast.error).mockReset();
    vi.spyOn(window, "prompt").mockRestore();
  });

  it("does not call the API when the typed confirmation does not match", () => {
    vi.spyOn(window, "prompt").mockReturnValue("reset default menu");
    renderOperations();

    fireEvent.click(screen.getByRole("button", { name: "Seed Default Menu" }));

    expect(mockPost).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith(
      "The menu was not reset because the confirmation did not match."
    );
  });

  it("posts the exact confirmation and reports the returned counts", async () => {
    vi.spyOn(window, "prompt").mockReturnValue("RESET DEFAULT MENU");
    mockPost.mockResolvedValue({
      data: { previousItemCount: 2, newItemCount: 45 },
    });
    const onMenuUpdated = renderOperations();

    fireEvent.click(screen.getByRole("button", { name: "Seed Default Menu" }));

    await waitFor(() =>
      expect(mockPost).toHaveBeenCalledWith("/admin/menu/reset-to-defaults", {
        confirmation: "RESET DEFAULT MENU",
      })
    );
    expect(toast.success).toHaveBeenCalledWith(
      "Default menu reset complete: 2 items replaced with 45 items."
    );
    expect(onMenuUpdated).toHaveBeenCalledOnce();
  });
});
