import axios from "axios";
import axiosInstance from "../axiosConfig";
import type { OrderDto } from "../types/api";

const ORDER_STATUS_UNAVAILABLE_CODE = "order_status_unavailable";

export function isOrderStatusUnavailable(error: unknown): boolean {
  if (!axios.isAxiosError(error) || error.response?.status !== 404) {
    return false;
  }

  const data: unknown = error.response.data;
  return (
    typeof data === "object" &&
    data !== null &&
    "code" in data &&
    data.code === ORDER_STATUS_UNAVAILABLE_CODE
  );
}

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
