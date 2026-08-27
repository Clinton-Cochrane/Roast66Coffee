import React from "react";
import { beforeEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { LanguageProvider } from "../../i18n/LanguageContext";
import About from "./About";

function renderAbout() {
  render(
    <LanguageProvider>
      <MemoryRouter>
        <About />
      </MemoryRouter>
    </LanguageProvider>
  );
}

describe("About", () => {
  beforeEach(() => window.localStorage.clear());

  it("explains the business and provides a clear event request path", () => {
    renderAbout();

    expect(
      screen.getByRole("heading", { level: 1, name: /coffee that shows up for your people/i })
    ).toBeInTheDocument();
    expect(screen.getByText(/mobile coffee trailer serving handcrafted drinks/i)).toBeInTheDocument();

    const requestLink = screen.getByRole("link", { name: /request the trailer/i });
    expect(requestLink).toHaveAttribute("href", "https://www.instagram.com/roast66coffee/");
    expect(requestLink).toHaveAttribute("target", "_blank");

    expect(screen.getByRole("heading", { name: /what to send us/i })).toBeInTheDocument();
    expect(screen.getByText(/preferred date and time/i)).toBeInTheDocument();
    expect(screen.getByText("Your estimated guest count")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /see what we serve/i })).toHaveAttribute(
      "href",
      "/menu"
    );
  });

  it("keeps the complete contact experience available in Spanish", () => {
    window.localStorage.setItem("roast66_locale", "es");
    renderAbout();

    expect(
      screen.getByRole("heading", { level: 1, name: /café que llega para reunir a los tuyos/i })
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /solicita el remolque/i })).toHaveAttribute(
      "href",
      "https://www.instagram.com/roast66coffee/"
    );
    expect(screen.getByRole("heading", { name: /qué debes enviarnos/i })).toBeInTheDocument();
    expect(screen.getByText("La cantidad estimada de invitados")).toBeInTheDocument();
  });
});
