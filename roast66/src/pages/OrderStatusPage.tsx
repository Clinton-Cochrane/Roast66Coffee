import React, { useEffect, useRef, useState, type FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import axios from "axios";
import axiosInstance from "../axiosConfig";
import { toast } from "react-toastify";
import OrderTracker from "../components/Customer/OrderTracker";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";
import { ORDER_STATUS } from "../constants/orderStatus";
import { getOrderStatusFromDto } from "../constants/orderStatusParse";
import {
  clearOrderStatusSession,
  readOrderStatusSession,
  writeOrderStatusSession,
} from "../constants/orderStatusSession";
import { useI18n } from "../i18n/LanguageContext";
import { fetchOrderLookup } from "../lib/orderStatusLookup";
import type { OrderDto, OrderLineItemDto } from "../types/api";

const ENABLE_STRIPE_PREPAY = import.meta.env.VITE_ENABLE_STRIPE_CHECKOUT === "true";

const STRIPE_PREPAY_RETURN_KEY = "stripePrepayReturn";

function OrderStatusPage() {
  const { locale, t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [orderId, setOrderId] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [prepayLoading, setPrepayLoading] = useState(false);
  const restoreRanRef = useRef(false);
  /** Bumps when a manual lookup starts so in-flight restore cannot overwrite state or sessionStorage. */
  const lookupEpochRef = useRef(0);
  /** Credentials last used for a successful load; polling reads this so typing in the form does not change poll requests. */
  const pollCredentialsRef = useRef<{ orderId: string; customerName: string } | null>(null);

  useEffect(() => {
    const checkout = searchParams.get("checkout");
    if (checkout !== "success" && checkout !== "cancelled") {
      return;
    }

    const raw = sessionStorage.getItem(STRIPE_PREPAY_RETURN_KEY);
    if (raw) {
      try {
        const parsed = JSON.parse(raw) as { customerName?: string; orderId?: number };
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

  useEffect(() => {
    const checkout = searchParams.get("checkout");
    if (checkout === "success" || checkout === "cancelled") {
      return;
    }
    if (restoreRanRef.current) {
      return;
    }
    restoreRanRef.current = true;

    const session = readOrderStatusSession();
    if (!session) {
      return;
    }

    setOrderId(session.orderId);
    setCustomerName(session.customerName);

    const runRestore = async () => {
      const epochAtRestoreStart = lookupEpochRef.current;
      setIsLoading(true);
      setOrder(null);
      try {
        const data = await fetchOrderLookup(parseInt(session.orderId, 10), session.customerName);
        if (lookupEpochRef.current !== epochAtRestoreStart) {
          return;
        }
        setOrder(data);
        setLastUpdatedAt(new Date());
        pollCredentialsRef.current = {
          orderId: session.orderId,
          customerName: session.customerName,
        };
        writeOrderStatusSession(
          session.orderId,
          session.customerName,
          getOrderStatusFromDto(data)
        );
      } catch (err: unknown) {
        if (
          lookupEpochRef.current === epochAtRestoreStart &&
          axios.isAxiosError(err) &&
          err.response?.status === 404
        ) {
          toast.error(t("orderStatus.restoreNotFound"));
        }
      } finally {
        if (lookupEpochRef.current === epochAtRestoreStart) {
          setIsLoading(false);
        }
      }
    };

    void runRestore();
  }, [searchParams]);

  const handleLookup = async (e: FormEvent) => {
    e.preventDefault();
    if (!orderId.trim() || !customerName.trim()) {
      toast.error(t("orderStatus.lookupMissingFields"));
      return;
    }
    lookupEpochRef.current += 1;
    pollCredentialsRef.current = null;
    setIsLoading(true);
    setOrder(null);
    try {
      const data = await fetchOrderLookup(parseInt(orderId, 10), customerName);
      setOrder(data);
      setLastUpdatedAt(new Date());
      pollCredentialsRef.current = {
        orderId: orderId.trim(),
        customerName: customerName.trim(),
      };
      writeOrderStatusSession(orderId, customerName, getOrderStatusFromDto(data));
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response?.status === 404) {
        clearOrderStatusSession();
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
      const { data } = await axiosInstance.post<{ checkoutUrl?: string }>(
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
    } catch (err: unknown) {
      sessionStorage.removeItem(STRIPE_PREPAY_RETURN_KEY);
      const msg = axios.isAxiosError(err)
        ? (err.response?.data as { message?: string })?.message ||
          (err.response?.status === 503
            ? t("orderStatus.paymentUnavailable")
            : t("orderStatus.paymentStartFailed"))
        : t("orderStatus.paymentStartFailed");
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

  const lineItems: OrderLineItemDto[] = order?.orderItems ?? order?.OrderItems ?? [];
  const statusValue = order ? getOrderStatusFromDto(order) : ORDER_STATUS.Received;
  const isCompleted = statusValue === ORDER_STATUS.Completed;

  /** Keep customer view in sync when staff advances status (admin). */
  useEffect(() => {
    if (!order) {
      pollCredentialsRef.current = null;
      return;
    }
    if (isCompleted) {
      return;
    }

    const creds = pollCredentialsRef.current;
    const id = creds?.orderId.trim() ?? "";
    const name = creds?.customerName.trim() ?? "";
    if (!id || !name) {
      return;
    }

    let cancelled = false;

    const refresh = async () => {
      if (cancelled) {
        return;
      }
      const epochAtRequest = lookupEpochRef.current;
      const active = pollCredentialsRef.current;
      const pollId = active?.orderId.trim() ?? "";
      const pollName = active?.customerName.trim() ?? "";
      if (!pollId || !pollName) {
        return;
      }
      try {
        const data = await fetchOrderLookup(parseInt(pollId, 10), pollName);
        if (cancelled || lookupEpochRef.current !== epochAtRequest) {
          return;
        }
        setOrder(data);
        setLastUpdatedAt(new Date());
        writeOrderStatusSession(pollId, pollName, getOrderStatusFromDto(data));
      } catch {
        /* ignore — user can use Check Status */
      }
    };

    const interval = window.setInterval(() => void refresh(), 45_000);
    const onVisibility = () => {
      if (document.visibilityState === "visible") {
        void refresh();
      }
    };
    document.addEventListener("visibilitychange", onVisibility);

    return () => {
      cancelled = true;
      window.clearInterval(interval);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [order?.id, isCompleted]);

  return (
    <div className="p-6 max-w-lg mx-auto">
      <h1 className="text-3xl font-bold mb-6">{t("orderStatus.title")}</h1>
      <p className="text-gray-600 mb-6">{t("orderStatus.subtitle")}</p>

      <form onSubmit={(e) => void handleLookup(e)} className="space-y-4 mb-8">
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

      {order ? (
        <div className="bg-gray-50 rounded-lg p-4">
          <p className="text-lg font-bold mb-2">
            {t("orderConfirmation.orderPrefix")} #{order.id}
          </p>
          <p className="text-gray-600 mb-4">
            {order.customerName} • {dateTimeFormatter.format(new Date(order.orderDate ?? ""))}
          </p>
          {isPaid ? (
            <p className="text-green-700 font-semibold mb-4">{t("orderStatus.paidOnline")}</p>
          ) : null}
          <ul className="space-y-1 mb-4">
            {lineItems.map((item, i) => (
              <li key={i}>
                {item.quantity}x{" "}
                {item.menuItem?.name ?? item.MenuItem?.name ?? t("orderStatus.itemFallback")}
              </li>
            ))}
          </ul>
          <div className="flex flex-wrap items-center gap-2 mb-4">
            <h2 className="text-xl font-bold">{t("orderStatus.statusHeader")}</h2>
            <span
              className={`text-xs font-semibold uppercase tracking-wide px-2 py-0.5 rounded ${
                isCompleted
                  ? "bg-gray-200 text-gray-600"
                  : "bg-emerald-100 text-emerald-800"
              }`}
            >
              {isCompleted ? t("orderStatus.trackingCompleteBadge") : t("orderStatus.liveBadge")}
            </span>
            {lastUpdatedAt ? (
              <span className="text-xs text-gray-400 w-full sm:w-auto sm:ml-auto">
                {t("orderStatus.lastUpdated", {
                  time: dateTimeFormatter.format(lastUpdatedAt),
                })}
              </span>
            ) : null}
          </div>
          <OrderTracker currentStatus={statusValue} />
          {ENABLE_STRIPE_PREPAY && !isPaid ? (
            <div className="mt-6 pt-4 border-t border-gray-200">
              <p className="text-gray-600 text-sm mb-3">{t("orderStatus.secureCheckoutDescription")}</p>
              <Button
                type="button"
                color="blue"
                onClick={() => void handlePrepay()}
                disabled={prepayLoading}
              >
                {prepayLoading ? t("orderStatus.redirecting") : t("orderStatus.prepayButton")}
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

export default OrderStatusPage;
