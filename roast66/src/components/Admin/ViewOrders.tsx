import React, { useCallback, useEffect, useMemo, useState } from "react";
import axios from "axios";
import axiosInstance from "../../axiosConfig";
import { toast } from "react-toastify";
import Card from "../common/Card";
import Button from "../common/Button";
import { ORDER_STATUS, type OrderStatusValue } from "../../constants/orderStatus";
import { getOrderStatusFromDto } from "../../constants/orderStatusParse";
import { useI18n } from "../../i18n/LanguageContext";
import type { NotificationLogEntry, OrderDto, OrderLineItemDto } from "../../types/api";

const POLL_INTERVAL_MS = 60000;

function orderStatusValue(order: OrderDto): OrderStatusValue {
  return getOrderStatusFromDto(order);
}

function orderId(order: OrderDto): number {
  return (order.id ?? order.Id) as number;
}

function orderDateMs(order: OrderDto): number {
  const raw = order.orderDate ?? order.OrderDate;
  if (raw == null || raw === "") return 0;
  const t = new Date(raw).getTime();
  return Number.isFinite(t) ? t : 0;
}

function ViewOrders() {
  const { t } = useI18n();
  const [orders, setOrders] = useState<OrderDto[]>([]);
  const [lastRefreshedAt, setLastRefreshedAt] = useState<Date | null>(null);
  const [newOrdersCount, setNewOrdersCount] = useState(0);
  const [lastNotifiedCount, setLastNotifiedCount] = useState(0);
  const [orderNotifications, setOrderNotifications] = useState<Record<number, NotificationLogEntry[]>>(
    {}
  );
  const [loadingNotificationsByOrderId, setLoadingNotificationsByOrderId] = useState<
    Record<number, boolean>
  >({});

  const statusLabelKeys = useMemo(
    () =>
      ({
        [ORDER_STATUS.Received]: "orderStatus.trackerReceivedLabel",
        [ORDER_STATUS.Preparing]: "orderStatus.trackerPreparingLabel",
        [ORDER_STATUS.ReadyForPickup]: "orderStatus.trackerReadyLabel",
        [ORDER_STATUS.Completed]: "orderStatus.trackerCompletedLabel",
      }) satisfies Record<OrderStatusValue, string>,
    []
  );

  const fetchOrders = useCallback(() => {
    axiosInstance
      .get<OrderDto[]>("/admin/orders")
      .then((response) => {
        setOrders(response.data);
        setLastRefreshedAt(new Date());
        setNewOrdersCount(0);
      })
      .catch((err: unknown) => {
        const status = axios.isAxiosError(err) ? err.response?.status : undefined;
        toast.error(
          status === 401 ? t("adminOrders.fetchOrders401") : t("adminOrders.fetchOrdersFailed")
        );
      });
  }, [t]);

  const fetchNewOrdersCount = useCallback(() => {
    if (!lastRefreshedAt) return;
    axiosInstance
      .get<{ count?: number }>("/admin/orders/new-count", {
        params: { since: lastRefreshedAt.toISOString() },
      })
      .then((response) => setNewOrdersCount(response.data.count ?? 0))
      .catch(() => {});
  }, [lastRefreshedAt]);

  useEffect(() => {
    fetchOrders();
  }, [fetchOrders]);

  useEffect(() => {
    if (!lastRefreshedAt) return;

    fetchNewOrdersCount();
    const interval = setInterval(fetchNewOrdersCount, POLL_INTERVAL_MS);

    const handleFocus = () => fetchNewOrdersCount();
    window.addEventListener("focus", handleFocus);

    return () => {
      clearInterval(interval);
      window.removeEventListener("focus", handleFocus);
    };
  }, [lastRefreshedAt, fetchNewOrdersCount]);

  useEffect(() => {
    if (newOrdersCount <= 0) {
      setLastNotifiedCount(0);
      return;
    }

    const canNotify =
      typeof Notification !== "undefined" && Notification.permission === "granted";
    if (canNotify && newOrdersCount > lastNotifiedCount) {
      new Notification(t("adminOrders.desktopNotificationTitle"), {
        body:
          newOrdersCount === 1
            ? t("adminOrders.desktopNotificationBodyOne")
            : t("adminOrders.desktopNotificationBodyMany", { count: newOrdersCount }),
      });
      setLastNotifiedCount(newOrdersCount);
    }
  }, [newOrdersCount, lastNotifiedCount, t]);

  const sortedOrders = useMemo(() => {
    const list = Array.isArray(orders) ? [...orders] : [];
    list.sort((a, b) => {
      const sa = orderStatusValue(a);
      const sb = orderStatusValue(b);
      const aDone = sa === ORDER_STATUS.Completed;
      const bDone = sb === ORDER_STATUS.Completed;
      if (aDone !== bDone) return aDone ? 1 : -1;
      const da = orderDateMs(a);
      const db = orderDateMs(b);
      return db - da;
    });
    return list;
  }, [orders]);

  const advanceStatus = (id: number) => {
    axiosInstance
      .put<{ newStatus?: string }>(`/admin/updateOrderStatus/${id}/status`)
      .then((res) => {
        const next = res.data?.newStatus;
        if (next === "Completed") {
          toast.success(t("adminOrders.orderMarkedComplete"));
        } else {
          toast.success(t("adminOrders.orderStatusUpdated"));
        }
        fetchOrders();
      })
      .catch((err: unknown) => {
        const data = axios.isAxiosError(err) ? err.response?.data : undefined;
        const message =
          typeof data === "string"
            ? data
            : data && typeof data === "object" && "message" in data
              ? String((data as { message?: string }).message)
              : axios.isAxiosError(err)
                ? err.response?.statusText
                : undefined;
        toast.error(message || t("adminOrders.failedUpdateStatus"));
      });
  };

  const fetchOrderNotifications = useCallback(
    (id: number) => {
      setLoadingNotificationsByOrderId((prev) => ({ ...prev, [id]: true }));
      axiosInstance
        .get<NotificationLogEntry[]>(`/admin/orders/${id}/notifications`)
        .then((response) => {
          setOrderNotifications((prev) => ({
            ...prev,
            [id]: Array.isArray(response.data) ? response.data : [],
          }));
        })
        .catch(() => {
          toast.error(t("adminOrders.failedLoadNotifications"));
        })
        .finally(() => {
          setLoadingNotificationsByOrderId((prev) => ({ ...prev, [id]: false }));
        });
    },
    [t]
  );

  const getStatusLabel = (status: number) => {
    const key = statusLabelKeys[status as OrderStatusValue];
    return key ? t(key) : t("adminOrders.statusUnknown");
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-3xl font-bold">{t("adminOrders.title")}</h1>
        <div className="relative inline-block">
          <Button onClick={fetchOrders} color="blue">
            {t("adminOrders.refresh")}
          </Button>
          {newOrdersCount > 0 && (
            <span
              className="absolute -top-1 -right-1 min-w-[1.25rem] h-5 px-1 inline-flex items-center justify-center bg-red-500 text-white text-xs font-bold rounded-full"
              aria-label={t("adminOrders.newOrdersBadgeAria", { count: newOrdersCount })}
            >
              {newOrdersCount > 99 ? t("adminOrders.badgeOver99") : newOrdersCount}
            </span>
          )}
        </div>
      </div>
      {orders.length === 0 ? (
        <p className="text-gray-500">{t("adminOrders.emptyState")}</p>
      ) : (
        <div className="space-y-4">
          {sortedOrders.map((order) => {
            const id = orderId(order);
            const status = orderStatusValue(order);
            const isComplete = status === ORDER_STATUS.Completed;
            const advanceLabel =
              status === ORDER_STATUS.ReadyForPickup
                ? t("adminOrders.advanceMarkComplete")
                : t("adminOrders.advanceStatus");
            const notifications = orderNotifications[id] ?? [];
            const loadingNotifications = Boolean(loadingNotificationsByOrderId[id]);
            const lineItems: OrderLineItemDto[] = order.orderItems || order.OrderItems || [];

            return (
              <Card key={id} title={t("adminOrders.orderCardTitle", { id })} className="mb-2">
                <div className="flex items-center gap-2 mb-4 flex-wrap">
                  <span
                    className={`px-2 py-1 rounded text-sm font-medium ${
                      isComplete ? "bg-green-200 text-green-800" : "bg-blue-100 text-blue-800"
                    }`}
                  >
                    {getStatusLabel(status)}
                  </span>
                  {isComplete ? (
                    <span className="text-sm font-medium text-green-800">
                      {t("adminOrders.completedNoAction")}
                    </span>
                  ) : (
                    <Button onClick={() => advanceStatus(id)} color="green">
                      {advanceLabel}
                    </Button>
                  )}
                  <Button onClick={() => fetchOrderNotifications(id)} color="gray">
                    {loadingNotifications ? t("adminOrders.loading") : t("adminOrders.refreshNotifications")}
                  </Button>
                </div>
                <p className="mb-1">
                  <strong>{t("adminOrders.customerLabel")}</strong>{" "}
                  {order.customerName ?? order.CustomerName}
                </p>
                {(order.customerPhone ?? order.CustomerPhone) ? (
                  <p className="mb-1">
                    <strong>{t("adminOrders.phoneLabel")}</strong>{" "}
                    {order.customerPhone ?? order.CustomerPhone}
                  </p>
                ) : null}
                <p className="mb-4">
                  <strong>{t("adminOrders.dateLabel")}</strong>{" "}
                  {new Date(order.orderDate ?? order.OrderDate ?? 0).toLocaleString()}
                </p>

                <ul className="space-y-2">
                  {lineItems.map((item, idx) => (
                    <li key={item.id ?? idx} className="flex flex-col border-b pb-2">
                      {item.menuItem?.name || item.MenuItem?.name ? (
                        <>
                          <span>
                            <strong>{t("adminOrders.itemLabel")}</strong>{" "}
                            {item.menuItem?.name ?? item.MenuItem?.name}
                          </span>
                          <span>
                            {" "}
                            <strong>{t("adminOrders.qtyLabel")}</strong> {item.quantity}
                          </span>
                          {item.notes ? (
                            <span>
                              {" "}
                              <strong>{t("adminOrders.notesLabel")}</strong> {item.notes}
                            </span>
                          ) : null}
                        </>
                      ) : (
                        <span className="text-red-500">{t("adminOrders.itemUnavailable")}</span>
                      )}
                    </li>
                  ))}
                </ul>
                <div className="mt-4 pt-3 border-t border-gray-200">
                  <p className="font-semibold mb-2">{t("adminOrders.notificationDelivery")}</p>
                  {notifications.length === 0 ? (
                    <p className="text-sm text-gray-500">{t("adminOrders.noNotificationsYet")}</p>
                  ) : (
                    <ul className="text-sm space-y-1">
                      {notifications.slice(0, 4).map((entry) => (
                        <li key={entry.id}>
                          {entry.recipientRole}: {entry.templateKey} -{" "}
                          <span className="font-medium">{entry.status}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default ViewOrders;
