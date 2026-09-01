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
export function tryGetOrderStatusFromDto(
  order: OrderDto | null | undefined
): OrderStatusValue | null {
  if (!order) {
    return null;
  }
  const raw: unknown = order.orderStatus ?? order.OrderStatus;
  if (typeof raw === "number" && Object.values(ORDER_STATUS).includes(raw as OrderStatusValue)) {
    return raw as OrderStatusValue;
  }
  if (typeof raw === "string") {
    const trimmed = raw.trim();
    if (trimmed === "") {
      return null;
    }
    const asNum = Number(trimmed);
    if (Object.values(ORDER_STATUS).includes(asNum as OrderStatusValue)) {
      return asNum as OrderStatusValue;
    }
    if (trimmed in NAME_TO_VALUE) {
      return NAME_TO_VALUE[trimmed];
    }
  }
  return null;
}

export function getOrderStatusFromDto(order: OrderDto | null | undefined): OrderStatusValue {
  return tryGetOrderStatusFromDto(order) ?? ORDER_STATUS.Received;
}
