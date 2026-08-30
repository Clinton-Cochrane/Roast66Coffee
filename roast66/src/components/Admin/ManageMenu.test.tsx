import React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import ManageMenu from "./ManageMenu";
import { LanguageProvider } from "../../i18n/LanguageContext";
import type { MenuItemDto } from "../../types/api";

const mockGet = vi.fn();
const mockPost = vi.fn();
const mockPut = vi.fn();
const mockDelete = vi.fn();

vi.mock("../../axiosConfig", () => ({
  default: {
    get: (...args: unknown[]) => mockGet(...args),
    post: (...args: unknown[]) => mockPost(...args),
    put: (...args: unknown[]) => mockPut(...args),
    delete: (...args: unknown[]) => mockDelete(...args),
  },
}));

vi.mock("react-toastify", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const menuItems: MenuItemDto[] = [
  {
    id: 7,
    name: "Blue Flame Nitro",
    description: "Nitro cold brew with sweet cream",
    price: 5.25,
    categoryType: 1,
    isFeaturedOnHome: true,
    isArchived: false,
    promotion: "10%",
  },
  {
    id: 8,
    name: "Retired Mocha",
    description: "No longer available",
    price: 4.75,
    categoryType: 0,
    isFeaturedOnHome: false,
    isArchived: true,
  },
];

const categories = [
  { id: 0, name: "Coffee" },
  { id: 1, name: "Specials" },
];

function setMatchMedia(matches: boolean) {
  Object.defineProperty(window, "matchMedia", {
    configurable: true,
    value: vi.fn().mockImplementation(() => ({
      matches,
      media: "(max-width: 960px)",
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

function renderManageMenu() {
  return render(
    <LanguageProvider>
      <ManageMenu />
    </LanguageProvider>
  );
}

describe("ManageMenu", () => {
  beforeEach(() => {
    setMatchMedia(false);
    mockGet.mockReset();
    mockPost.mockReset();
    mockPut.mockReset();
    mockDelete.mockReset();
    mockGet.mockImplementation((url: string) =>
      Promise.resolve({ data: url === "/admin/categories" ? categories : menuItems })
    );
    mockPost.mockResolvedValue({});
    mockPut.mockResolvedValue({});
    mockDelete.mockResolvedValue({});
  });

  it("shows all menu controls in a two-row item and defaults the sticky editor to create", async () => {
    renderManageMenu();

    const itemName = await screen.findByText("Blue Flame Nitro");
    const row = itemName.closest("li");
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText("Nitro cold brew with sweet cream")).toBeInTheDocument();
    expect(within(row as HTMLElement).getByRole("button", { name: /daily special/i })).toBeInTheDocument();
    expect(within(row as HTMLElement).getByRole("button", { name: /menu special/i })).toBeInTheDocument();
    expect(within(row as HTMLElement).getByLabelText(/promo/i)).toHaveValue("10%");
    expect(screen.getByRole("heading", { name: "Create new" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Permanently delete" })).not.toBeInTheDocument();
    expect(screen.queryByText("Retired Mocha")).not.toBeInTheDocument();
  });

  it("loads one item into the editor with archive and protected permanent deletion", async () => {
    renderManageMenu();
    const editButton = await screen.findByRole("button", { name: "Edit Blue Flame Nitro" });

    fireEvent.click(editButton);

    expect(screen.getByRole("heading", { name: "Blue Flame Nitro" })).toBeInTheDocument();
    expect(screen.getByLabelText("Name")).toHaveValue("Blue Flame Nitro");
    expect(screen.getByLabelText("Price")).toHaveValue(5.25);
    expect(screen.getByRole("button", { name: "Archive" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Permanently delete" })).toBeDisabled();
    expect(editButton).toHaveAttribute("aria-pressed", "true");
  });

  it("permanently deletes only after the exact item name is entered", async () => {
    renderManageMenu();
    fireEvent.click(await screen.findByRole("button", { name: "Edit Blue Flame Nitro" }));
    const deleteButton = screen.getByRole("button", { name: "Permanently delete" });
    fireEvent.change(screen.getByLabelText(/type blue flame nitro/i), {
      target: { value: "Blue Flame Nitro" },
    });
    fireEvent.click(deleteButton);

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith("/admin/menu/7"));
  });

  it("shows archived items separately and restores them", async () => {
    renderManageMenu();
    await screen.findByText("Blue Flame Nitro");

    fireEvent.click(screen.getByRole("button", { name: "Archived" }));
    const archivedName = await screen.findByText("Retired Mocha");
    const row = archivedName.closest("li");
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText("Archived")).toBeInTheDocument();
    expect(within(row as HTMLElement).getByRole("button", { name: /daily special/i })).toBeDisabled();

    fireEvent.click(within(row as HTMLElement).getByRole("button", { name: /edit retired mocha/i }));
    fireEvent.click(screen.getByRole("button", { name: "Restore" }));

    await waitFor(() => expect(mockPut).toHaveBeenCalledWith("/admin/menu/8/restore"));
  });

  it("opens and closes the editor as a mobile bottom sheet", async () => {
    setMatchMedia(true);
    renderManageMenu();
    const editButton = await screen.findByRole("button", { name: "Edit Blue Flame Nitro" });

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    fireEvent.click(editButton);

    const dialog = screen.getByRole("dialog", { name: "Blue Flame Nitro" });
    expect(dialog).toBeInTheDocument();
    fireEvent.click(
      within(dialog).getByRole("button", { name: "Close menu editor", hidden: true })
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(editButton).toHaveFocus();
  });
});
