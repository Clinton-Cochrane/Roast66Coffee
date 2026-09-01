/** Loose API shapes (backend may use PascalCase or camelCase). */
export type MenuItemDto = {
  id: number;
  name: string;
  price: number;
  description: string;
  categoryType: number;
  isFeaturedOnHome: boolean;
  isArchived: boolean;
  effectivePrice?: number;
  promotion?: string | null;
  promotionType?: number | null;
  promotionValue?: number | null;
};

export type OrderLineAddOnDto = {
  id?: number;
  quantity: number;
  itemName?: string;
  ItemName?: string;
  menuItem?: { name?: string; price?: number };
  MenuItem?: { name?: string; price?: number };
};

export type OrderLineItemDto = {
  id?: number;
  quantity?: number;
  notes?: string;
  itemName?: string;
  ItemName?: string;
  menuItem?: { name?: string; price?: number };
  MenuItem?: { name?: string; price?: number };
  /** Present on some API responses for order lines with flavor add-ons */
  addOns?: OrderLineAddOnDto[];
  AddOns?: OrderLineAddOnDto[];
};

export type OrderDto = {
  id?: number;
  Id?: number;
  trackingToken?: string;
  TrackingToken?: string;
  customerName?: string;
  CustomerName?: string;
  customerPhone?: string | null;
  CustomerPhone?: string | null;
  customerEmail?: string | null;
  CustomerEmail?: string | null;
  customerNotificationOptIn?: boolean;
  CustomerNotificationOptIn?: boolean;
  orderDate?: string;
  OrderDate?: string;
  orderStatus?: number;
  OrderStatus?: number;
  orderItems?: OrderLineItemDto[];
  OrderItems?: OrderLineItemDto[];
  paidUtc?: string | null;
  PaidUtc?: string | null;
  paymentProvider?: string | null;
  PaymentProvider?: string | null;
  completedUtc?: string | null;
  CompletedUtc?: string | null;
};

export type AdminOrderHistoryResponse = {
  items: OrderDto[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
};

export type NotificationLogEntry = {
  id: number;
  recipientRole: string;
  templateKey: string;
  status: string;
};
