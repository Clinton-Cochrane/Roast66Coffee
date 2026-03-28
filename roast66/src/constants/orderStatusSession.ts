/**
 * Persists successful order-status lookups so customers can return via the nav dot
 * without re-entering order id and name (same tab: sessionStorage).
 */

export const ORDER_STATUS_LOOKUP_SESSION_KEY = "roast66_orderStatusLookup";

export const ORDER_STATUS_SESSION_UPDATED_EVENT = "roast66:orderStatusSessionUpdated";

export type OrderStatusLookupSessionPayload = {
  orderId: string;
  customerName: string;
  /** From last successful lookup; drives nav dot color (e.g. completed = muted). */
  orderStatus?: number;
};

function parsePayload(raw: string): OrderStatusLookupSessionPayload | null {
  try {
    const parsed = JSON.parse(raw) as Partial<OrderStatusLookupSessionPayload>;
    const orderId = typeof parsed.orderId === "string" ? parsed.orderId.trim() : "";
    const customerName =
      typeof parsed.customerName === "string" ? parsed.customerName.trim() : "";
    if (!orderId || !customerName) {
      return null;
    }
    const orderStatus =
      typeof parsed.orderStatus === "number" && Number.isFinite(parsed.orderStatus)
        ? parsed.orderStatus
        : undefined;
    return { orderId, customerName, orderStatus };
  } catch {
    return null;
  }
}

export function readOrderStatusSession(): OrderStatusLookupSessionPayload | null {
  if (typeof window === "undefined") {
    return null;
  }
  const raw = sessionStorage.getItem(ORDER_STATUS_LOOKUP_SESSION_KEY);
  if (!raw) {
    return null;
  }
  return parsePayload(raw);
}

export function writeOrderStatusSession(
  orderId: string,
  customerName: string,
  orderStatus?: number
): void {
  if (typeof window === "undefined") {
    return;
  }
  const payload: OrderStatusLookupSessionPayload = {
    orderId: orderId.trim(),
    customerName: customerName.trim(),
  };
  if (orderStatus !== undefined) {
    payload.orderStatus = orderStatus;
  }
  sessionStorage.setItem(ORDER_STATUS_LOOKUP_SESSION_KEY, JSON.stringify(payload));
  window.dispatchEvent(new CustomEvent(ORDER_STATUS_SESSION_UPDATED_EVENT));
}

export function clearOrderStatusSession(): void {
  if (typeof window === "undefined") {
    return;
  }
  sessionStorage.removeItem(ORDER_STATUS_LOOKUP_SESSION_KEY);
  window.dispatchEvent(new CustomEvent(ORDER_STATUS_SESSION_UPDATED_EVENT));
}
