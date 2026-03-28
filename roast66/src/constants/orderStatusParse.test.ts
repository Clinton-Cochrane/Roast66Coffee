import { describe, it, expect } from "vitest";
import { ORDER_STATUS } from "./orderStatus";
import { getOrderStatusFromDto } from "./orderStatusParse";
import type { OrderDto } from "../types/api";

describe("getOrderStatusFromDto", () => {
  it("returns Received when order is null or undefined", () => {
    expect(getOrderStatusFromDto(null)).toBe(ORDER_STATUS.Received);
    expect(getOrderStatusFromDto(undefined)).toBe(ORDER_STATUS.Received);
  });

  it("reads numeric camelCase and PascalCase", () => {
    expect(getOrderStatusFromDto({ orderStatus: 3 } as OrderDto)).toBe(ORDER_STATUS.Completed);
    expect(getOrderStatusFromDto({ OrderStatus: 2 } as OrderDto)).toBe(ORDER_STATUS.ReadyForPickup);
    expect(
      getOrderStatusFromDto({ orderStatus: 1, OrderStatus: 3 } as OrderDto)
    ).toBe(ORDER_STATUS.Preparing);
  });

  it("parses numeric strings and enum names", () => {
    expect(getOrderStatusFromDto({ orderStatus: "3" } as unknown as OrderDto)).toBe(
      ORDER_STATUS.Completed
    );
    expect(getOrderStatusFromDto({ orderStatus: "Completed" } as unknown as OrderDto)).toBe(
      ORDER_STATUS.Completed
    );
    expect(getOrderStatusFromDto({ orderStatus: "ReadyForPickup" } as unknown as OrderDto)).toBe(
      ORDER_STATUS.ReadyForPickup
    );
  });
});
