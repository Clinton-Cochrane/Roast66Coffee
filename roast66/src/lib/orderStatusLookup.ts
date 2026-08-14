import axiosInstance from "../axiosConfig";
import type { OrderDto } from "../types/api";

/**
 * Token-authenticated public tracking, shared by restore and polling.
 */
export async function fetchOrderLookup(
  trackingToken: string
): Promise<OrderDto> {
  const { data } = await axiosInstance.get<OrderDto>(
    `/order/track/${encodeURIComponent(trackingToken.trim())}`
  );
  return data;
}
