import React, { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import axios from "../axiosConfig";
import { toast } from "react-toastify";
import OrderTracker from "../components/Customer/OrderTracker";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";
import { useI18n } from "../i18n/LanguageContext";

const ENABLE_STRIPE_PREPAY =
  process.env.REACT_APP_ENABLE_STRIPE_CHECKOUT === "true";

const STRIPE_PREPAY_RETURN_KEY = "stripePrepayReturn";

function OrderStatusPage() {
  const { locale, t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [orderId, setOrderId] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [order, setOrder] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [prepayLoading, setPrepayLoading] = useState(false);

  useEffect(() => {
    const checkout = searchParams.get("checkout");
    if (checkout !== "success" && checkout !== "cancelled") {
      return;
    }

    const raw = sessionStorage.getItem(STRIPE_PREPAY_RETURN_KEY);
    if (raw) {
      try {
        const parsed = JSON.parse(raw);
        if (parsed.customerName) setCustomerName(parsed.customerName);
        if (parsed.orderId != null) setOrderId(String(parsed.orderId));
      } catch {
        /* ignore */
      }
      sessionStorage.removeItem(STRIPE_PREPAY_RETURN_KEY);
    }

    if (checkout === "success") {
      toast.success(t("orderStatus.paymentReceived"));
    } else {
      toast.info(t("orderStatus.paymentCancelled"));
    }

    const next = new URLSearchParams(searchParams);
    next.delete("checkout");
    next.delete("orderId");
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams, t]);

  const handleLookup = async (e) => {
    e.preventDefault();
    if (!orderId.trim() || !customerName.trim()) {
      toast.error(t("orderStatus.lookupMissingFields"));
      return;
    }
    setIsLoading(true);
    setOrder(null);
    try {
      const { data } = await axios.get("/order/lookup", {
        params: { orderId: parseInt(orderId, 10), customerName: customerName.trim() },
      });
      setOrder(data);
    } catch (err) {
      if (err.response?.status === 404) {
        toast.error(t("orderStatus.notFound"));
      } else {
        toast.error(t("orderStatus.lookupFailed"));
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handlePrepay = async () => {
    if (!order) return;
    const idempotencyKey =
      window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`;
    sessionStorage.setItem(
      STRIPE_PREPAY_RETURN_KEY,
      JSON.stringify({ customerName: customerName.trim(), orderId: order.id })
    );
    setPrepayLoading(true);
    try {
      const { data } = await axios.post(
        "/payments/checkout-session",
        {
          existingOrderId: order.id,
          customerName: order.customerName,
          customerPhone: order.customerPhone ?? "",
          orderItems: [],
        },
        { headers: { "X-Idempotency-Key": idempotencyKey } }
      );
      const checkoutUrl = data?.checkoutUrl;
      if (!checkoutUrl) {
        throw new Error(t("order.checkoutMissingUrl"));
      }
      window.location.assign(checkoutUrl);
    } catch (err) {
      sessionStorage.removeItem(STRIPE_PREPAY_RETURN_KEY);
      const msg =
        err.response?.data?.message ||
        (err.response?.status === 503
          ? t("orderStatus.paymentUnavailable")
          : t("orderStatus.paymentStartFailed"));
      toast.error(msg);
    } finally {
      setPrepayLoading(false);
    }
  };

  const isPaid = Boolean(order?.paidUtc ?? order?.PaidUtc);
  const dateTimeFormatter = new Intl.DateTimeFormat(locale, {
    dateStyle: "medium",
    timeStyle: "short",
  });

  return (
    <div className="p-6 max-w-lg mx-auto">
      <h1 className="text-3xl font-bold mb-6">{t("orderStatus.title")}</h1>
      <p className="text-gray-600 mb-6">
        {t("orderStatus.subtitle")}
      </p>

      <form onSubmit={handleLookup} className="space-y-4 mb-8">
        <FormInput
          type="text"
          name="orderId"
          placeholder={t("orderStatus.orderIdPlaceholder")}
          title={t("orderStatus.orderIdPlaceholder")}
          aria-label={t("orderStatus.orderIdPlaceholder")}
          value={orderId}
          onChange={(e) => setOrderId(e.target.value)}
        />
        <FormInput
          type="text"
          name="customerName"
          placeholder={t("order.namePlaceholder")}
          title={t("order.namePlaceholder")}
          aria-label={t("order.namePlaceholder")}
          value={customerName}
          onChange={(e) => setCustomerName(e.target.value)}
        />
        <Button type="submit" color="green" disabled={isLoading}>
          {isLoading ? t("orderStatus.lookingUp") : t("orderStatus.lookup")}
        </Button>
      </form>

      {order && (
        <div className="bg-gray-50 rounded-lg p-4">
          <p className="text-lg font-bold mb-2">Order #{order.id}</p>
          <p className="text-gray-600 mb-4">
            {order.customerName} • {dateTimeFormatter.format(new Date(order.orderDate))}
          </p>
          {isPaid && (
            <p className="text-green-700 font-semibold mb-4">
              {t("orderStatus.paidOnline")}
            </p>
          )}
          <ul className="space-y-1 mb-4">
            {(order.orderItems ?? order.OrderItems ?? []).map((item, i) => (
              <li key={i}>
                {item.quantity}x {item.menuItem?.name ?? t("orderStatus.itemFallback")}
              </li>
            ))}
          </ul>
          <h2 className="text-xl font-bold mb-4">{t("orderStatus.statusHeader")}</h2>
          <OrderTracker currentStatus={order.orderStatus ?? 0} />
          {ENABLE_STRIPE_PREPAY && !isPaid && (
            <div className="mt-6 pt-4 border-t border-gray-200">
              <p className="text-gray-600 text-sm mb-3">
                {t("orderStatus.secureCheckoutDescription")}
              </p>
              <Button
                type="button"
                color="blue"
                onClick={handlePrepay}
                disabled={prepayLoading}
              >
                {prepayLoading ? t("orderStatus.redirecting") : t("orderStatus.prepayButton")}
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default OrderStatusPage;
