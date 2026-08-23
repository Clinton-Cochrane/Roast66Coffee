import type { MenuItemDto } from "../../types/api";

export const effectivePrice = (item: MenuItemDto) => item.effectivePrice ?? item.price;

export default function PromotionPrice({ item, className = "" }: { item: MenuItemDto; className?: string }) {
  if (!item.promotion) return <span className={className}>${item.price.toFixed(2)}</span>;
  return (
    <span className={className}>
      <span className="mr-2 rounded bg-[#a64b2a] px-2 py-0.5 text-xs text-white">{item.promotion} off</span>
      <span className="mr-2 text-sm text-[#6f5b4b] line-through">${item.price.toFixed(2)}</span>
      <span>${effectivePrice(item).toFixed(2)}</span>
    </span>
  );
}
