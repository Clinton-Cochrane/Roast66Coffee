import { describe, it, expect } from "vitest";
import CategoryType from "../constants/categories";
import { canOrderMenuItemDirectly } from "./canOrderMenuItemDirectly";

describe("canOrderMenuItemDirectly", () => {
  it("returns true for coffee, drinks, and specials", () => {
    expect(canOrderMenuItemDirectly({ categoryType: CategoryType.COFFEE })).toBe(true);
    expect(canOrderMenuItemDirectly({ categoryType: CategoryType.DRINKS })).toBe(true);
    expect(canOrderMenuItemDirectly({ categoryType: CategoryType.SPECIALS })).toBe(true);
  });

  it("returns false for flavors and invalid input", () => {
    expect(canOrderMenuItemDirectly({ categoryType: CategoryType.FLAVORS })).toBe(false);
    expect(canOrderMenuItemDirectly(null)).toBe(false);
    expect(canOrderMenuItemDirectly({})).toBe(false);
  });
});
