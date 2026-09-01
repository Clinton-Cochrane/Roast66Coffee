import React, { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
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
import {
  fetchOrderLookup,
  isOrderStatusUnavailable,
} from "../lib/orderStatusLookup";
import type { OrderDto, OrderLineItemDto } from "../types/api";

const ENABLE_ONLINE_PREPAY =
  import.meta.env.VITE_ENABLE_ONLINE_PAYMENTS === "true" ||
  import.meta.env.VITE_ENABLE_STRIPE_CHECKOUT === "true";

const PAYMENT_PREPAY_RETURN_KEY = "paymentPrepayReturn";
const LEGACY_STRIPE_PREPAY_RETURN_KEY = "stripePrepayReturn";

/**
 * Restores and polls a private token-authenticated order. The lookup epoch makes
 * manual lookup authoritative over older restore/poll requests, preventing late
 * responses from replacing the visible order or its sessionStorage record.
 */
function OrderStatusPage() {
  const { locale, t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [trackingToken, setTrackingToken] = useState(() => searchParams.get("token") ?? "");
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isUnavailable, setIsUnavailable] = useState(false);
  const [prepayLoading, setPrepayLoading] = useState(false);
  const restoreRanRef = useRef(false);
  /** Bumps when a manual lookup starts so in-flight restore cannot overwrite state or sessionStorage. */
  const lookupEpochRef = useRef(0);
  const pollTokenRef = useRef<string | null>(null);

  const showUnavailable = useCallback(
    (failedToken: string) => {
      lookupEpochRef.current += 1;
      pollTokenRef.current = null;
      setIsLoading(false);
      setOrder(null);
      setLastUpdatedAt(null);
      setIsUnavailable(true);
      setTrackingToken("");
      clearOrderStatusSession(failedToken);
      setSearchParams(
        (current) => {
          if (!current.has("token")) {
            return current;
          }
          const next = new URLSearchParams(current);
          next.delete("token");
          return next;
        },
        { replace: true }
      );
    },
    [setSearchParams]
  );

  // Provider redirects are reduced to a toast plus the stored private token;
  // transient query parameters are removed so refresh does not replay the toast.
  useEffect(() => {
    const checkout = searchParams.get("checkout");
    if (checkout !== "success" && checkout !== "cancelled") {
      return;
    }

    const raw =
      sessionStorage.getItem(PAYMENT_PREPAY_RETURN_KEY) ??
      sessionStorage.getItem(LEGACY_STRIPE_PREPAY_RETURN_KEY);
    if (raw) {
      try {
        const parsed = JSON.parse(raw) as { trackingToken?: string };
        if (parsed.trackingToken) setTrackingToken(parsed.trackingToken);
      } catch {
        /* ignore */
      }
      sessionStorage.removeItem(PAYMENT_PREPAY_RETURN_KEY);
      sessionStorage.removeItem(LEGACY_STRIPE_PREPAY_RETURN_KEY);
    }

    if (checkout === "success") {
      toast.success(t("orderStatus.paymentSubmitted"));
    } else {
      toast.info(t("orderStatus.paymentCancelled"));
    }

    const next = new URLSearchParams(searchParams);
    next.delete("checkout");
    next.delete("token");
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams, t]);

  // Restore once from the URL or this tab's last successful tracking session.
  // A checkout return is handled by the preceding effect to avoid competing reads.
  useEffect(() => {
    const checkout = searchParams.get("checkout");
    if (checkout === "success" || checkout === "cancelled") {
      return;
    }
    if (restoreRanRef.current) {
      return;
    }
    restoreRanRef.current = true;

    const sessionToken = searchParams.get("token") ?? readOrderStatusSession()?.trackingToken;
    if (!sessionToken) {
      return;
    }

    setTrackingToken(sessionToken);

    const runRestore = async () => {
      const epochAtRestoreStart = lookupEpochRef.current;
      setIsLoading(true);
      setOrder(null);
      setLastUpdatedAt(null);
      setIsUnavailable(false);
      try {
        const data = await fetchOrderLookup(sessionToken);
        if (lookupEpochRef.current !== epochAtRestoreStart) {
          return;
        }
        setOrder(data);
        setLastUpdatedAt(new Date());
        setIsUnavailable(false);
        pollTokenRef.current = sessionToken;
        writeOrderStatusSession(sessionToken, getOrderStatusFromDto(data));
      } catch (err: unknown) {
        if (lookupEpochRef.current !== epochAtRestoreStart) {
          return;
        }
        if (isOrderStatusUnavailable(err)) {
          showUnavailable(sessionToken);
        } else {
          toast.error(t("orderStatus.lookupFailed"));
        }
      } finally {
        if (lookupEpochRef.current === epochAtRestoreStart) {
          setIsLoading(false);
        }
      }
    };

    void runRestore();
  }, [searchParams, showUnavailable, t]);

  const handleLookup = async (e: FormEvent) => {
    e.preventDefault();
    if (!trackingToken.trim()) {
      toast.error(t("orderStatus.lookupMissingFields"));
      return;
    }
    const requestedToken = trackingToken.trim();
    lookupEpochRef.current += 1;
    pollTokenRef.current = null;
    setIsLoading(true);
    setOrder(null);
    setLastUpdatedAt(null);
    setIsUnavailable(false);
    try {
      const data = await fetchOrderLookup(requestedToken);
      setOrder(data);
      setLastUpdatedAt(new Date());
      setIsUnavailable(false);
      pollTokenRef.current = requestedToken;
      writeOrderStatusSession(requestedToken, getOrderStatusFromDto(data));
    } catch (err: unknown) {
      if (isOrderStatusUnavailable(err)) {
        showUnavailable(requestedToken);
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
      PAYMENT_PREPAY_RETURN_KEY,
      JSON.stringify({ trackingToken: trackingToken.trim() })
    );
    setPrepayLoading(true);
    try {
      const { data } = await axiosInstance.post<{ checkoutUrl?: string }>(
        "/payments/checkout-session",
        {
          existingOrderId: order.id,
          customerName: order.customerName,
          customerPhone: order.customerPhone ?? "",
        },
        { headers: { "X-Idempotency-Key": idempotencyKey } }
      );
      const checkoutUrl = data?.checkoutUrl;
      if (!checkoutUrl) {
        throw new Error(t("order.checkoutMissingUrl"));
      }
      window.location.assign(checkoutUrl);
    } catch (err: unknown) {
      sessionStorage.removeItem(PAYMENT_PREPAY_RETURN_KEY);
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

  /**
   * Keep the customer view in sync while an order is active. Visibility refresh
   * avoids waiting for the timer after a sleeping mobile tab becomes active.
   */
  useEffect(() => {
    if (!order) {
      pollTokenRef.current = null;
      return;
    }
    if (isCompleted) {
      return;
    }

    const token = pollTokenRef.current?.trim() ?? "";
    if (!token) {
      return;
    }

    let cancelled = false;

    const refresh = async () => {
      if (cancelled) {
        return;
      }
      const epochAtRequest = lookupEpochRef.current;
      const pollToken = pollTokenRef.current?.trim() ?? "";
      if (!pollToken) {
        return;
      }
      try {
        const data = await fetchOrderLookup(pollToken);
        if (cancelled || lookupEpochRef.current !== epochAtRequest) {
          return;
        }
        setOrder(data);
        setLastUpdatedAt(new Date());
        writeOrderStatusSession(pollToken, getOrderStatusFromDto(data));
      } catch (err: unknown) {
        if (
          !cancelled &&
          lookupEpochRef.current === epochAtRequest &&
          isOrderStatusUnavailable(err)
        ) {
          showUnavailable(pollToken);
        }
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
  }, [order?.id, isCompleted, showUnavailable]);

  return (
    <div className="p-6 max-w-lg mx-auto">
      <h1 className="text-3xl font-bold mb-6">{t("orderStatus.title")}</h1>
      <p className="text-gray-600 mb-6">{t("orderStatus.subtitle")}</p>

      <form onSubmit={(e) => void handleLookup(e)} className="space-y-4 mb-8">
        <FormInput
          type="text"
          name="trackingToken"
          placeholder={t("orderStatus.orderIdPlaceholder")}
          title={t("orderStatus.orderIdPlaceholder")}
          aria-label={t("orderStatus.orderIdPlaceholder")}
          value={trackingToken}
          onChange={(e) => setTrackingToken(e.target.value)}
        />
        <Button type="submit" color="green" disabled={isLoading}>
          {isLoading ? t("orderStatus.lookingUp") : t("orderStatus.lookup")}
        </Button>
      </form>

      {isUnavailable ? (
        <p
          role="status"
          className="mb-8 rounded-lg border border-gray-200 bg-gray-50 p-4 text-gray-700"
        >
          {t("orderStatus.unavailable")}
        </p>
      ) : null}

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
              className={`text-sm font-semibold uppercase tracking-wide px-2 py-0.5 rounded ${
                isCompleted
                  ? "bg-gray-200 text-gray-600"
                  : "bg-emerald-100 text-emerald-800"
              }`}
            >
              {isCompleted ? t("orderStatus.trackingCompleteBadge") : t("orderStatus.liveBadge")}
            </span>
            {lastUpdatedAt ? (
              <span className="text-sm text-gray-500 w-full sm:w-auto sm:ml-auto">
                {t("orderStatus.lastUpdated", {
                  time: dateTimeFormatter.format(lastUpdatedAt),
                })}
              </span>
            ) : null}
          </div>
          <OrderTracker currentStatus={statusValue} />
          {ENABLE_ONLINE_PREPAY && !isPaid ? (
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
