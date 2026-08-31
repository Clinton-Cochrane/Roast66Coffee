import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  clearOrderIdempotencyKey,
  getOrCreateOrderIdempotencyKey,
  PENDING_ORDER_SUBMISSION_KEY,
} from "./orderSubmissionIdempotency";

describe("order submission idempotency", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.restoreAllMocks();
  });

  it("reuses the pending key for an unchanged retry", () => {
    const payload = { customerName: "Ada", orderItems: [{ menuItemId: 1, quantity: 1 }] };

    const first = getOrCreateOrderIdempotencyKey(payload);
    const retry = getOrCreateOrderIdempotencyKey(payload);

    expect(retry).toBe(first);
    expect(sessionStorage.getItem(PENDING_ORDER_SUBMISSION_KEY)).toContain(first);
  });

  it("creates a new key when the customer changes the payload", () => {
    const first = getOrCreateOrderIdempotencyKey({ quantity: 1 });
    const changed = getOrCreateOrderIdempotencyKey({ quantity: 2 });

    expect(changed).not.toBe(first);
  });

  it("clears only the matching successful submission", () => {
    const key = getOrCreateOrderIdempotencyKey({ quantity: 1 });

    clearOrderIdempotencyKey("another-key");
    expect(sessionStorage.getItem(PENDING_ORDER_SUBMISSION_KEY)).not.toBeNull();

    clearOrderIdempotencyKey(key);
    expect(sessionStorage.getItem(PENDING_ORDER_SUBMISSION_KEY)).toBeNull();
    expect(getOrCreateOrderIdempotencyKey({ quantity: 1 })).not.toBe(key);
  });
});
