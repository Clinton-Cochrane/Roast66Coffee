import axiosInstance from "../axiosConfig";
import type { OrderDto } from "../types/api";

/**
 * GET /order/lookup — shared by manual submit, session restore, and future polling/SSE.
 */
export async function fetchOrderLookup(
  orderId: number,
  customerName: string
): Promise<OrderDto> {
  const { data } = await axiosInstance.get<OrderDto>("/order/lookup", {
    params: { orderId, customerName: customerName.trim() },
  });
  return data;
}
