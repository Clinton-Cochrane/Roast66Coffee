import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import Loading from "./Loading";

describe("Loading", () => {
  it("renders loading indicator", () => {
    const { container } = render(<Loading />);
    const svg = container.querySelector("svg");
    expect(svg).toBeInTheDocument();
  });

  it("renders coffee emoji in loading animation", () => {
    render(<Loading />);
    expect(screen.getAllByText("☕").length).toBeGreaterThanOrEqual(1);
  });
});
