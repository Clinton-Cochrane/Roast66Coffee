/**
 * Order status stages for the tracker (Domino's-style).
 */
export const ORDER_STATUS = {
  Received: 0,
  Preparing: 1,
  ReadyForPickup: 2,
  Completed: 3,
} as const;

export type OrderStatusValue = (typeof ORDER_STATUS)[keyof typeof ORDER_STATUS];

export const ORDER_STATUS_LABELS: Record<OrderStatusValue, string> = {
  [ORDER_STATUS.Received]: "Order received",
  [ORDER_STATUS.Preparing]: "Preparing your drinks",
  [ORDER_STATUS.ReadyForPickup]: "Ready for pickup",
  [ORDER_STATUS.Completed]: "Order complete",
};

export const ORDER_STATUS_DESCRIPTIONS: Record<OrderStatusValue, string> = {
  [ORDER_STATUS.Received]: "We've got your order and will start soon.",
  [ORDER_STATUS.Preparing]: "Our baristas are making your drinks.",
  [ORDER_STATUS.ReadyForPickup]: "Your order is ready! Come pick it up.",
  [ORDER_STATUS.Completed]: "Thanks for visiting Roast 66!",
};
