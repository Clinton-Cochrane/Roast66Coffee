import React from "react";
import { useLocation, useNavigate, Link } from "react-router-dom";
import OrderTracker from "../components/Customer/OrderTracker";
import Button from "../components/common/Button";
import { useI18n } from "../i18n/LanguageContext";
import { getOrderStatusFromDto } from "../constants/orderStatusParse";
import type { OrderDto, OrderLineItemDto } from "../types/api";

function OrderConfirmationPage() {
  const { locale, t } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const order = (location.state as { order?: OrderDto } | null)?.order;
  const customerEmail = order?.customerEmail ?? order?.CustomerEmail ?? "";
  const notificationOptIn = Boolean(
    order?.customerNotificationOptIn ?? order?.CustomerNotificationOptIn
  );
  const hasEmailUpdates = notificationOptIn && customerEmail.trim().length > 0;

  const currencyFormatter = new Intl.NumberFormat(locale, {
    style: "currency",
    currency: "USD",
  });

  if (!order) {
    return (
      <div className="p-6 max-w-lg mx-auto">
        <h1 className="text-2xl font-bold mb-4">{t("orderConfirmation.fallbackTitle")}</h1>
        <p className="text-[#5b4940] mb-4">
          {t("orderConfirmation.fallbackDescriptionStart")}{" "}
          <Link to="/order-status" className="text-[#6c89a2] underline">
            {t("orderStatus.title")}
          </Link>{" "}
          {t("orderConfirmation.fallbackDescriptionEnd")}
        </p>
        <Button onClick={() => navigate("/order-status")} color="green">
          {t("orderConfirmation.fallbackButton")}
        </Button>
      </div>
    );
  }

  const items: OrderLineItemDto[] = order.orderItems ?? order.OrderItems ?? [];
  const total = items.reduce(
    (sum, item) =>
      sum +
      (item.menuItem?.price ?? item.MenuItem?.price ?? 0) * (item.quantity ?? 1) +
      (item.addOns ?? []).reduce(
        (aSum, addOn) =>
          aSum + (addOn.menuItem?.price ?? addOn.MenuItem?.price ?? 0) * (addOn.quantity ?? 1),
        0
      ),
    0
  );

  const handleDownloadSummary = () => {
    const summary = {
      orderNumber: order.id ?? order.Id,
      customerName: order.customerName ?? order.CustomerName,
      customerPhone: order.customerPhone ?? order.CustomerPhone,
      trackerUrl: `${window.location.origin}/order-status`,
      items: items.map((item) => ({
        name: item.menuItem?.name ?? item.MenuItem?.name ?? t("orderConfirmation.itemFallback"),
        quantity: item.quantity ?? 1,
        addOns: (item.addOns ?? []).map((a) => ({
          name: a.menuItem?.name ?? a.MenuItem?.name ?? t("orderConfirmation.addOnFallback"),
          quantity: a.quantity ?? 1,
        })),
        notes: item.notes ?? "",
      })),
      total,
    };

    const blob = new Blob([JSON.stringify(summary, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `roast66-order-${order.id ?? order.Id}.json`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const orderId = order.id ?? order.Id ?? 0;
  const statusVal = getOrderStatusFromDto(order);

  return (
    <div className="p-6 max-w-lg mx-auto">
      <h1 className="text-3xl md:text-4xl font-bold mb-2 tracking-[0.01em] text-[#4a3326]">
        {t("orderConfirmation.title")}
      </h1>
      <p className="text-[#5b4940] mb-6">
        {t("orderConfirmation.thankYou", {
          customerName: String(order.customerName ?? order.CustomerName ?? ""),
        })}
      </p>

      <div className="r66-panel rounded-lg p-4 mb-6">
        <p className="text-lg font-bold mb-2">
          {t("orderConfirmation.orderPrefix")} #{orderId}
        </p>
        <ul className="space-y-1 mb-4">
          {items.map((item, i) => (
            <li key={i}>
              {item.quantity}x{" "}
              {item.menuItem?.name ?? item.MenuItem?.name ?? t("orderConfirmation.itemFallback")}
              {(item.addOns || []).length > 0
                ? `${t("orderConfirmation.addOnsLeadIn")}${(item.addOns ?? [])
                    .map((a) => a.menuItem?.name ?? a.MenuItem?.name)
                    .filter(Boolean)
                    .join(t("orderConfirmation.addOnListSeparator"))}`
                : ""}
              {item.notes
                ? t("orderConfirmation.notesSuffix", { notes: item.notes })
                : ""}
            </li>
          ))}
        </ul>
        <p className="font-bold">
          {t("orderConfirmation.total")}: {currencyFormatter.format(total)}
        </p>
        {hasEmailUpdates ? (
          <p className="text-sm text-[#5b4940] mt-2">
            {t("orderConfirmation.emailUpdates")} <strong>{customerEmail}</strong>.
          </p>
        ) : (
          <div className="mt-3">
            <p className="text-sm text-[#5b4940] mb-2">{t("orderConfirmation.emailOptional")}</p>
            <Button color="gray" onClick={handleDownloadSummary}>
              {t("orderConfirmation.downloadSummary")}
            </Button>
          </div>
        )}
      </div>

      <h2 className="text-xl font-bold mb-4 text-[#4a3326]">{t("orderConfirmation.statusTitle")}</h2>
      <OrderTracker currentStatus={Number(statusVal)} />

      <div className="mt-8 pt-6 border-t border-[#ddcdbf]">
        <p className="text-sm text-[#5b4940] mb-2">
          {t("orderConfirmation.laterStatusStart")}{" "}
          <Link to="/order-status" className="text-[#6c89a2] underline">
            {t("orderStatus.title")}
          </Link>{" "}
          {t("orderConfirmation.laterStatusEnd", { orderId })}
        </p>
        <Link to="/menu">
          <Button color="blue">{t("orderConfirmation.backToMenu")}</Button>
        </Link>
      </div>
    </div>
  );
}

export default OrderConfirmationPage;
