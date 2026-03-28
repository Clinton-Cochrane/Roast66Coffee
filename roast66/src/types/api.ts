/** Loose API shapes (backend may use PascalCase or camelCase). */
export type MenuItemDto = {
  id: number;
  name: string;
  price: number;
  description: string;
  categoryType: number;
};

export type OrderLineAddOnDto = {
  id?: number;
  quantity: number;
  menuItem?: { name?: string; price?: number };
  MenuItem?: { name?: string; price?: number };
};

export type OrderLineItemDto = {
  id?: number;
  quantity?: number;
  notes?: string;
  menuItem?: { name?: string; price?: number };
  MenuItem?: { name?: string; price?: number };
  /** Present on some API responses for order lines with flavor add-ons */
  addOns?: OrderLineAddOnDto[];
};

export type OrderDto = {
  id?: number;
  Id?: number;
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
};

export type NotificationLogEntry = {
  id: number;
  recipientRole: string;
  templateKey: string;
  status: string;
};
