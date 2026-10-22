import React from "react";
import { existsSync } from "node:fs";
import { URL } from "node:url";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { LanguageProvider } from "../../i18n/LanguageContext";
import HomeMarketingLocation from "./HomeMarketingLocation";

describe("HomeMarketingLocation", () => {
  it("ships every responsive trailer photo and the JPEG fallback", () => {
    render(
      <LanguageProvider>
        <HomeMarketingLocation />
      </LanguageProvider>
    );

    const photo = screen.getByRole("img", {
      name: "Roast 66 Coffee trailer set up at a community pop-up",
    });
    expect(photo).toHaveAttribute("src", "/images/home/marketing-960.jpg");

    const sources = photo.closest("picture")!.querySelectorAll("source");
    expect(sources).toHaveLength(2);

    const imagePaths = [
      photo.getAttribute("src")!,
      ...Array.from(sources).flatMap((source) =>
        source.srcset.split(",").map((entry) => entry.trim().split(" ")[0])
      ),
    ];
    expect(imagePaths).toHaveLength(7);

    for (const imagePath of imagePaths) {
      const file = new URL(`../../../public${imagePath}`, import.meta.url);
      expect(existsSync(file), `Missing trailer photo: ${imagePath}`).toBe(true);
    }
  });
});
