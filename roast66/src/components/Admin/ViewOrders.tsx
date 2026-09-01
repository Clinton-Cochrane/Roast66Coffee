import React, { useCallback, useEffect, useMemo, useState } from "react";
import axios from "axios";
import axiosInstance from "../../axiosConfig";
import { toast } from "react-toastify";
import Card from "../common/Card";
import Button from "../common/Button";
import { ORDER_STATUS, type OrderStatusValue } from "../../constants/orderStatus";
import { getOrderStatusFromDto } from "../../constants/orderStatusParse";
import { useI18n } from "../../i18n/LanguageContext";
import type {
  AdminOrderHistoryResponse,
  NotificationLogEntry,
  OrderDto,
  OrderLineItemDto,
} from "../../types/api";
import "../../styles/Admin.css";

const POLL_INTERVAL_MS = 60000;

type OrderFilters = {
  status: string;
  search: string;
  fromDate: string;
  toDate: string;
};

const EMPTY_FILTERS: OrderFilters = {
  status: "all",
  search: "",
  fromDate: "",
  toDate: "",
};

const EMPTY_PAGE: AdminOrderHistoryResponse = {
  items: [],
  page: 1,
  pageSize: 50,
  totalItems: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false,
};

function orderStatusValue(order: OrderDto): OrderStatusValue {
  return getOrderStatusFromDto(order);
}

function orderId(order: OrderDto): number {
  return (order.id ?? order.Id) as number;
}

function paymentProviderLabel(order: OrderDto): string {
  const provider = (order.paymentProvider ?? order.PaymentProvider ?? "online").trim();
  return provider.charAt(0).toUpperCase() + provider.slice(1);
}

function ViewOrders() {
  const { t } = useI18n();
  const [orders, setOrders] = useState<OrderDto[]>([]);
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<AdminOrderHistoryResponse>(EMPTY_PAGE);
  const [draftFilters, setDraftFilters] = useState<OrderFilters>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<OrderFilters>(EMPTY_FILTERS);
  const [loadingOrders, setLoadingOrders] = useState(true);
  const [hasLoadedOrders, setHasLoadedOrders] = useState(false);
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
    const params: Record<string, string | number> = {
      page,
      status: appliedFilters.status,
    };
    if (appliedFilters.search.trim()) params.search = appliedFilters.search.trim();
    if (appliedFilters.fromDate) {
      params.fromUtc = new Date(`${appliedFilters.fromDate}T00:00:00`).toISOString();
    }
    if (appliedFilters.toDate) {
      const end = new Date(`${appliedFilters.toDate}T00:00:00`);
      end.setDate(end.getDate() + 1);
      params.toUtc = end.toISOString();
    }

    setLoadingOrders(true);
    return axiosInstance
      .get<AdminOrderHistoryResponse>("/admin/orders", { params })
      .then((response) => {
        const data = response.data;
        if (page > 1 && data.totalPages > 0 && page > data.totalPages) {
          setPage(data.totalPages);
          return;
        }
        setOrders(Array.isArray(data.items) ? data.items : []);
        setPagination(data);
        setLastRefreshedAt(new Date());
        setNewOrdersCount(0);
        setHasLoadedOrders(true);
      })
      .catch((err: unknown) => {
        const status = axios.isAxiosError(err) ? err.response?.status : undefined;
        toast.error(
          status === 401 ? t("adminOrders.fetchOrders401") : t("adminOrders.fetchOrdersFailed")
        );
      })
      .finally(() => setLoadingOrders(false));
  }, [appliedFilters, page, t]);

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

  const applyFilters = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setPage(1);
    setAppliedFilters({ ...draftFilters, search: draftFilters.search.trim() });
  };

  const clearFilters = () => {
    setDraftFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setPage(1);
  };

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
      <div className="flex items-center justify-between gap-4 mb-6 flex-wrap">
        <h1 className="text-3xl font-bold">{t("adminOrders.title")}</h1>
        <div className="relative inline-block">
          <Button onClick={() => void fetchOrders()} color="blue" disabled={loadingOrders}>
            {loadingOrders && hasLoadedOrders
              ? t("adminOrders.refreshing")
              : t("adminOrders.refresh")}
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

      <form
        className="r66-admin-order-filters grid gap-3 rounded-lg border border-gray-200 bg-white p-4 md:grid-cols-2 lg:grid-cols-4"
        onSubmit={applyFilters}
      >
        <label className="flex flex-col gap-1 font-semibold">
          {t("adminOrders.statusFilterLabel")}
          <select
            className="rounded border border-gray-300 px-3 py-2 font-normal"
            value={draftFilters.status}
            onChange={(event) =>
              setDraftFilters((current) => ({ ...current, status: event.target.value }))
            }
          >
            <option value="all">{t("adminOrders.statusAll")}</option>
            <option value="active">{t("adminOrders.statusActive")}</option>
            <option value="received">{t("orderStatus.trackerReceivedLabel")}</option>
            <option value="preparing">{t("orderStatus.trackerPreparingLabel")}</option>
            <option value="readyForPickup">{t("orderStatus.trackerReadyLabel")}</option>
            <option value="completed">{t("orderStatus.trackerCompletedLabel")}</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 font-semibold">
          {t("adminOrders.searchLabel")}
          <input
            className="rounded border border-gray-300 px-3 py-2 font-normal"
            type="search"
            value={draftFilters.search}
            placeholder={t("adminOrders.searchPlaceholder")}
            onChange={(event) =>
              setDraftFilters((current) => ({ ...current, search: event.target.value }))
            }
          />
        </label>
        <label className="flex flex-col gap-1 font-semibold">
          {t("adminOrders.fromDateLabel")}
          <input
            className="rounded border border-gray-300 px-3 py-2 font-normal"
            type="date"
            value={draftFilters.fromDate}
            onChange={(event) =>
              setDraftFilters((current) => ({ ...current, fromDate: event.target.value }))
            }
          />
        </label>
        <label className="flex flex-col gap-1 font-semibold">
          {t("adminOrders.toDateLabel")}
          <input
            className="rounded border border-gray-300 px-3 py-2 font-normal"
            type="date"
            value={draftFilters.toDate}
            onChange={(event) =>
              setDraftFilters((current) => ({ ...current, toDate: event.target.value }))
            }
          />
        </label>
        <div className="flex items-center gap-3 md:col-span-2 lg:col-span-4">
          <Button type="submit" color="blue" disabled={loadingOrders}>
            {t("adminOrders.applyFilters")}
          </Button>
          <Button onClick={clearFilters} color="gray" disabled={loadingOrders}>
            {t("adminOrders.clearFilters")}
          </Button>
        </div>
        <p className="text-sm text-gray-600 md:col-span-2 lg:col-span-4">
          {t("adminOrders.retentionNote")}
        </p>
      </form>

      {loadingOrders && !hasLoadedOrders ? (
        <p className="text-gray-500" role="status">
          {t("adminOrders.loadingOrders")}
        </p>
      ) : orders.length === 0 ? (
        <p className="text-gray-500">{t("adminOrders.emptyFilteredState")}</p>
      ) : (
        <div className="space-y-4">
          {orders.map((order) => {
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
            const isPaid = Boolean(order.paidUtc ?? order.PaidUtc);

            return (
              <Card
                key={id}
                title={t("adminOrders.orderCardTitle", { id })}
                className={`mb-2 ${isComplete ? "r66-admin-completed-order" : ""}`}
              >
                <div className="flex items-center gap-2 mb-4 flex-wrap">
                  <span
                    className={`px-2 py-1 rounded text-sm font-medium ${
                      isComplete ? "bg-green-200 text-green-800" : "bg-blue-100 text-blue-800"
                    }`}
                  >
                    {getStatusLabel(status)}
                  </span>
                  {isPaid ? (
                    <span className="px-2 py-1 rounded bg-emerald-100 text-emerald-800 text-sm font-semibold">
                      {t("adminOrders.paidWithProvider", {
                        provider: paymentProviderLabel(order),
                      })}
                    </span>
                  ) : null}
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
                <div className={isComplete ? "r66-admin-completed-copy" : undefined}>
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
                    {lineItems.map((item, idx) => {
                      const addOns = item.addOns ?? item.AddOns ?? [];

                      return (
                        <li key={item.id ?? idx} className="flex flex-col border-b pb-2">
                          {item.itemName || item.ItemName || item.menuItem?.name || item.MenuItem?.name ? (
                            <>
                              <span>
                                <strong>{t("adminOrders.itemLabel")}</strong>{" "}
                                {item.itemName ?? item.ItemName ?? item.menuItem?.name ?? item.MenuItem?.name}
                              </span>
                              <span>
                                {" "}
                                <strong>{t("adminOrders.qtyLabel")}</strong> {item.quantity}
                              </span>
                              {addOns.length > 0 ? (
                                <span>
                                  <strong>{t("adminOrders.shotsLabel")}</strong>{" "}
                                  {addOns
                                    .map((addOn) => {
                                      const name =
                                        addOn.itemName ??
                                        addOn.ItemName ??
                                        addOn.menuItem?.name ??
                                        addOn.MenuItem?.name;
                                      return name ? `${name} × ${addOn.quantity}` : null;
                                    })
                                    .filter(Boolean)
                                    .join(", ")}
                                </span>
                              ) : null}
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
                      );
                    })}
                  </ul>
                  <div className="mt-4 pt-3 border-t border-gray-200">
                    <p className="font-semibold mb-2">{t("adminOrders.notificationDelivery")}</p>
                    {notifications.length === 0 ? (
                      <p className="text-sm">{t("adminOrders.noNotificationsYet")}</p>
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
                </div>
              </Card>
            );
          })}
        </div>
      )}

      {hasLoadedOrders && pagination.totalItems > 0 ? (
        <nav
          className="flex items-center justify-between gap-3 border-t border-gray-200 pt-4 flex-wrap"
          aria-label={t("adminOrders.paginationAria")}
        >
          <p>
            {t("adminOrders.pageSummary", {
              page: pagination.page,
              totalPages: pagination.totalPages,
              totalItems: pagination.totalItems,
            })}
          </p>
          <div className="flex gap-2">
            <Button
              onClick={() => setPage((current) => Math.max(1, current - 1))}
              color="gray"
              disabled={!pagination.hasPreviousPage || loadingOrders}
            >
              {t("adminOrders.previous")}
            </Button>
            <Button
              onClick={() => setPage((current) => current + 1)}
              color="blue"
              disabled={!pagination.hasNextPage || loadingOrders}
            >
              {t("adminOrders.next")}
            </Button>
          </div>
        </nav>
      ) : null}
    </div>
  );
}

export default ViewOrders;
