import { describe, it, expect, beforeEach, vi } from "vitest";
import {
  ORDER_STATUS_LOOKUP_SESSION_KEY,
  ORDER_STATUS_SESSION_UPDATED_EVENT,
  clearOrderStatusSession,
  readOrderStatusSession,
  writeOrderStatusSession,
} from "./orderStatusSession";

describe("orderStatusSession", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.restoreAllMocks();
  });

  it("returns null when key is missing", () => {
    expect(readOrderStatusSession()).toBeNull();
  });

  it("parses valid payload with optional orderStatus", () => {
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: " token-7 ", orderStatus: 3 })
    );
    expect(readOrderStatusSession()).toEqual({
      trackingToken: "token-7",
      orderStatus: 3,
    });
  });

  it("returns null when trackingToken is empty after trim", () => {
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ trackingToken: "" })
    );
    expect(readOrderStatusSession()).toBeNull();
  });

  it("writeOrderStatusSession dispatches custom event", () => {
    const spy = vi.fn();
    window.addEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);
    writeOrderStatusSession("token-1", 2);
    expect(spy).toHaveBeenCalledTimes(1);
    expect(JSON.parse(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)!)).toEqual({
      trackingToken: "token-1",
      orderStatus: 2,
    });
    window.removeEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);
  });

  it("clearOrderStatusSession removes key and dispatches event", () => {
    writeOrderStatusSession("token-1");
    const spy = vi.fn();
    window.addEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);
    clearOrderStatusSession();
    expect(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)).toBeNull();
    expect(spy).toHaveBeenCalled();
    window.removeEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);
  });

  it("only clears a session when the expected tracking token matches", () => {
    writeOrderStatusSession("new-token", 1);
    const spy = vi.fn();
    window.addEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);

    clearOrderStatusSession("stale-token");
    expect(readOrderStatusSession()?.trackingToken).toBe("new-token");
    expect(spy).not.toHaveBeenCalled();

    clearOrderStatusSession("new-token");
    expect(readOrderStatusSession()).toBeNull();
    expect(spy).toHaveBeenCalledTimes(1);
    window.removeEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);
  });
});
