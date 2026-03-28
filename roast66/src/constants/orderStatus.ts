/**
 * Order status stages for the tracker (Domino's-style).
 * User-facing labels live in i18n strings (`orderStatus.*` keys), not here.
 */
export const ORDER_STATUS = {
  Received: 0,
  Preparing: 1,
  ReadyForPickup: 2,
  Completed: 3,
} as const;

export type OrderStatusValue = (typeof ORDER_STATUS)[keyof typeof ORDER_STATUS];
