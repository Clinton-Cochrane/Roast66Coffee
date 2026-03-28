import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import Card from "./Card";

describe("Card", () => {
  it("renders children", () => {
    render(
      <Card>
        <p>Card content</p>
      </Card>
    );
    expect(screen.getByText("Card content")).toBeInTheDocument();
  });

  it("renders title when provided", () => {
    render(
      <Card title="Menu Item">
        <p>Content</p>
      </Card>
    );
    expect(screen.getByText("Menu Item")).toBeInTheDocument();
    expect(screen.getByText("Content")).toBeInTheDocument();
  });

  it("renders without title when not provided", () => {
    render(
      <Card>
        <p>Content only</p>
      </Card>
    );
    expect(screen.getByText("Content only")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 2 })).not.toBeInTheDocument();
  });
});
