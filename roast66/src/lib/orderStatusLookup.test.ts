import { describe, expect, it } from "vitest";
import { isOrderStatusUnavailable } from "./orderStatusLookup";

function axiosError(status: number, code?: string): unknown {
  return {
    isAxiosError: true,
    response: {
      status,
      data: code ? { code } : {},
    },
  };
}

describe("isOrderStatusUnavailable", () => {
  it("recognizes the stable unavailable tracking response", () => {
    expect(isOrderStatusUnavailable(axiosError(404, "order_status_unavailable"))).toBe(
      true
    );
  });

  it("does not treat unrelated or transient failures as unavailable", () => {
    expect(isOrderStatusUnavailable(axiosError(404))).toBe(false);
    expect(isOrderStatusUnavailable(axiosError(404, "different_error"))).toBe(false);
    expect(isOrderStatusUnavailable(axiosError(429, "order_status_unavailable"))).toBe(
      false
    );
    expect(isOrderStatusUnavailable(axiosError(503))).toBe(false);
    expect(isOrderStatusUnavailable(new Error("offline"))).toBe(false);
  });
});
