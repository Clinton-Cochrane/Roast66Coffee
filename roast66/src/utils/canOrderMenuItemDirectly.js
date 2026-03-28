import CategoryType from "../constants/categories";

/**
 * Menu items that may appear as their own order line (not flavor add-ons only).
 */
export function canOrderMenuItemDirectly(item) {
  if (!item || item.categoryType == null) {
    return false;
  }
  return (
    item.categoryType === CategoryType.COFFEE ||
    item.categoryType === CategoryType.DRINKS ||
    item.categoryType === CategoryType.SPECIALS
  );
}
