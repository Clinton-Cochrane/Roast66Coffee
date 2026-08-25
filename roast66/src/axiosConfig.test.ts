import { describe, it, expect, vi, beforeEach } from "vitest";

const requestHolder: { fn?: (config: Record<string, unknown>) => Record<string, unknown> } = {};
const responseHolder: { fn?: (error: unknown) => unknown } = {};

vi.mock("axios", () => ({
  default: {
    create: () => ({
      interceptors: {
        request: {
          use: (onFulfilled: (config: Record<string, unknown>) => Record<string, unknown>) => {
            requestHolder.fn = onFulfilled;
          },
        },
        response: {
          use: (_onOk: unknown, onRejected: (error: unknown) => unknown) => {
            responseHolder.fn = onRejected;
          },
        },
      },
    }),
  },
}));

describe("axiosConfig interceptors", () => {
  beforeEach(async () => {
    vi.unstubAllEnvs();
    vi.resetModules();
    localStorage.clear();
    requestHolder.fn = undefined;
    responseHolder.fn = undefined;
    await import("./axiosConfig");
  });

  it("adds bearer token for protected requests", () => {
    localStorage.setItem("token", "x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
    const config = { method: "get", url: "/admin/orders", headers: {} };
    const result = requestHolder.fn!(config) as {
      headers: Record<string, unknown>;
    };
    expect(result.headers.Authorization).toBe("Bearer x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
  });

  it("does not add token for public order POST requests", () => {
    localStorage.setItem("token", "x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
    const config = { method: "post", url: "/order", headers: {} };
    const result = requestHolder.fn!(config) as {
      headers: Record<string, unknown>;
    };
    expect(result.headers.Authorization).toBeUndefined();
  });

  it("serves menu reads from the public snapshot in static test mode", async () => {
    vi.stubEnv("VITE_USE_STATIC_MENU", "true");
    vi.resetModules();
    requestHolder.fn = undefined;
    responseHolder.fn = undefined;
    await import("./axiosConfig");

    const result = requestHolder.fn!({
      method: "get",
      url: "/menu",
      baseURL: "https://example.com/api",
      headers: {},
    });

    expect(result.baseURL).toBeUndefined();
    expect(result.url).toBe("/data/menu.json");
  });

  it("clears token and redirects cash users on 401", async () => {
    localStorage.setItem("token", "x.eyJleHAiOjQxMDI0NDQ4MDB9.x");
    const assignSpy = vi.fn();
    Object.defineProperty(window, "location", {
      configurable: true,
      value: {
        pathname: "/cash/orders",
        assign: assignSpy,
      },
    });

    await expect(responseHolder.fn!({ response: { status: 401 } })).rejects.toEqual({
      response: { status: 401 },
    });
    expect(localStorage.getItem("token")).toBeNull();
    expect(assignSpy).toHaveBeenCalledWith("/cash");
  });
});
