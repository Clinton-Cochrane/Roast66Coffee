import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import FormInput from "./FormInput";

describe("FormInput", () => {
  it("renders with value and placeholder", () => {
    render(
      <FormInput name="username" value="" onChange={() => {}} placeholder="Enter username" />
    );
    const input = screen.getByPlaceholderText("Enter username");
    expect(input).toBeInTheDocument();
    expect(input).toHaveValue("");
  });

  it("calls onChange when value changes", () => {
    const handleChange = vi.fn();
    render(
      <FormInput name="username" value="" onChange={handleChange} placeholder="Username" />
    );
    fireEvent.change(screen.getByPlaceholderText("Username"), {
      target: { value: "admin" },
    });
    expect(handleChange).toHaveBeenCalledTimes(1);
  });

  it("renders with type password", () => {
    render(
      <FormInput
        name="password"
        type="password"
        value=""
        onChange={() => {}}
        placeholder="Password"
      />
    );
    expect(screen.getByPlaceholderText("Password")).toHaveAttribute("type", "password");
  });

  it("has required attribute when required prop is true", () => {
    render(
      <FormInput name="email" value="" onChange={() => {}} placeholder="Email" required />
    );
    expect(screen.getByPlaceholderText("Email")).toBeRequired();
  });
});
