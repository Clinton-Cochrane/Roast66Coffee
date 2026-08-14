import React from "react";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import Welcome from "./Welcome";
import { LanguageProvider } from "../../i18n/LanguageContext";

describe("Welcome", () => {
  it("presents one primary heading and direct menu and order actions", () => {
    render(
      <LanguageProvider>
        <MemoryRouter>
          <Welcome />
        </MemoryRouter>
      </LanguageProvider>
    );

    expect(
      screen.getByRole("heading", {
        level: 1,
        name: "Fresh coffee, wherever the road takes you.",
      })
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /order now/i })).toHaveAttribute("href", "/order");
    expect(screen.getByRole("link", { name: /view menu/i })).toHaveAttribute("href", "/menu");
  });
});
