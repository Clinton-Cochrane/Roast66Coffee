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
      JSON.stringify({ orderId: " 7 ", customerName: " Pat ", orderStatus: 3 })
    );
    expect(readOrderStatusSession()).toEqual({
      orderId: "7",
      customerName: "Pat",
      orderStatus: 3,
    });
  });

  it("returns null when orderId or customerName is empty after trim", () => {
    sessionStorage.setItem(
      ORDER_STATUS_LOOKUP_SESSION_KEY,
      JSON.stringify({ orderId: "", customerName: "x" })
    );
    expect(readOrderStatusSession()).toBeNull();
  });

  it("writeOrderStatusSession dispatches custom event", () => {
    const spy = vi.fn();
    window.addEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);
    writeOrderStatusSession("1", "Ada", 2);
    expect(spy).toHaveBeenCalledTimes(1);
    expect(JSON.parse(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)!)).toEqual({
      orderId: "1",
      customerName: "Ada",
      orderStatus: 2,
    });
    window.removeEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);
  });

  it("clearOrderStatusSession removes key and dispatches event", () => {
    writeOrderStatusSession("1", "Ada");
    const spy = vi.fn();
    window.addEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);
    clearOrderStatusSession();
    expect(sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY)).toBeNull();
    expect(spy).toHaveBeenCalled();
    window.removeEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, spy);
  });
});
