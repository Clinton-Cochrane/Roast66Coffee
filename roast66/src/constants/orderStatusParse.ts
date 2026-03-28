import type { OrderDto } from "../types/api";
import { ORDER_STATUS, type OrderStatusValue } from "./orderStatus";

const NAME_TO_VALUE: Record<string, OrderStatusValue> = {
  Received: ORDER_STATUS.Received,
  Preparing: ORDER_STATUS.Preparing,
  ReadyForPickup: ORDER_STATUS.ReadyForPickup,
  Completed: ORDER_STATUS.Completed,
};

/**
 * Resolves order status from API payloads that may use camelCase, PascalCase,
 * numeric strings, or enum name strings (defensive — backend uses numeric JSON).
 */
export function getOrderStatusFromDto(order: OrderDto | null | undefined): OrderStatusValue {
  if (!order) {
    return ORDER_STATUS.Received;
  }
  const raw: unknown = order.orderStatus ?? order.OrderStatus;
  if (typeof raw === "number" && Number.isFinite(raw)) {
    return raw as OrderStatusValue;
  }
  if (typeof raw === "string") {
    const trimmed = raw.trim();
    if (trimmed === "") {
      return ORDER_STATUS.Received;
    }
    const asNum = Number(trimmed);
    if (!Number.isNaN(asNum)) {
      return asNum as OrderStatusValue;
    }
    if (trimmed in NAME_TO_VALUE) {
      return NAME_TO_VALUE[trimmed];
    }
  }
  return ORDER_STATUS.Received;
}
