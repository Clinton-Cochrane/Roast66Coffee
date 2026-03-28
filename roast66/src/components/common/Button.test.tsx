import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import Button from "./Button";

describe("Button", () => {
  it("renders children", () => {
    render(<Button>Click me</Button>);
    expect(screen.getByRole("button", { name: /click me/i })).toBeInTheDocument();
  });

  it("calls onClick when clicked", () => {
    const handleClick = vi.fn();
    render(<Button onClick={handleClick}>Submit</Button>);
    fireEvent.click(screen.getByRole("button", { name: /submit/i }));
    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it("does not call onClick when disabled", () => {
    const handleClick = vi.fn();
    render(
      <Button onClick={handleClick} disabled>
        Submit
      </Button>
    );
    fireEvent.click(screen.getByRole("button", { name: /submit/i }));
    expect(handleClick).not.toHaveBeenCalled();
  });

  it("renders with type submit", () => {
    render(<Button type="submit">Submit</Button>);
    expect(screen.getByRole("button")).toHaveAttribute("type", "submit");
  });

  it("renders link variant without solid button background classes", () => {
    render(
      <Button variant="link" color="green">
        Order this item
      </Button>
    );
    const btn = screen.getByRole("button", { name: /order this item/i });
    expect(btn.className).toContain("bg-transparent");
    expect(btn.className).toContain("underline");
    expect(btn.className).not.toContain("shadow-[0_2px_0");
  });
});
