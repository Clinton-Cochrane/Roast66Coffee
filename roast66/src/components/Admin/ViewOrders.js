// src/components/Admin/ViewOrders.jsx
import React, { useCallback, useEffect, useMemo, useState } from "react";
import axios from "../../axiosConfig";
import { toast } from "react-toastify";
import Card from "../common/Card";
import Button from "../common/Button";
import { ORDER_STATUS, ORDER_STATUS_LABELS } from "../../constants/orderStatus";

const STATUS_STAGES = ["Received", "Preparing", "ReadyForPickup", "Completed"];
const POLL_INTERVAL_MS = 60000;

function orderStatusValue(order) {
  const raw = order.orderStatus ?? order.OrderStatus;
  if (typeof raw === "string" && raw !== "" && !Number.isNaN(Number(raw))) {
    return Number(raw);
  }
  return Number(raw);
}

function orderId(order) {
  return order.id ?? order.Id;
}

/** Milliseconds since epoch for sorting; invalid/missing dates sort as 0 (stable fallback). */
function orderDateMs(order) {
  const raw = order.orderDate ?? order.OrderDate;
  if (raw == null || raw === "") return 0;
  const t = new Date(raw).getTime();
  return Number.isFinite(t) ? t : 0;
}

function ViewOrders() {
  const [orders, setOrders] = useState([]);
  const [lastRefreshedAt, setLastRefreshedAt] = useState(null);
  const [newOrdersCount, setNewOrdersCount] = useState(0);
  const [lastNotifiedCount, setLastNotifiedCount] = useState(0);
  const [orderNotifications, setOrderNotifications] = useState({});
  const [loadingNotificationsByOrderId, setLoadingNotificationsByOrderId] =
    useState({});

  const fetchOrders = useCallback(() => {
    axios
      .get("/admin/orders")
      .then((response) => {
        setOrders(response.data);
        setLastRefreshedAt(new Date());
        setNewOrdersCount(0);
      })
      .catch((err) => {
        const status = err.response?.status;
        toast.error(
          status === 401
            ? "Not signed in or session expired — sign out and log in again."
            : "Failed to fetch orders"
        );
      });
  }, []);

  const fetchNewOrdersCount = useCallback(() => {
    if (!lastRefreshedAt) return;
    axios
      .get("/admin/orders/new-count", {
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
      new Notification("Roast66 new orders", {
        body:
          newOrdersCount === 1
            ? "1 new order is waiting."
            : `${newOrdersCount} new orders are waiting.`,
      });
      setLastNotifiedCount(newOrdersCount);
    }
  }, [newOrdersCount, lastNotifiedCount]);

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

  const advanceStatus = (id) => {
    axios
      .put(`/admin/updateOrderStatus/${id}/status`)
      .then((res) => {
        const next = res.data?.newStatus;
        if (next === "Completed") {
          toast.success("Order marked complete.");
        } else {
          toast.success("Order status updated.");
        }
        fetchOrders();
      })
      .catch((err) => {
        const data = err.response?.data;
        const message =
          typeof data === "string"
            ? data
            : data?.message ?? err.response?.statusText;
        toast.error(message || "Failed to update status");
      });
  };

  const fetchOrderNotifications = useCallback((id) => {
    setLoadingNotificationsByOrderId((prev) => ({ ...prev, [id]: true }));
    axios
      .get(`/admin/orders/${id}/notifications`)
      .then((response) => {
        setOrderNotifications((prev) => ({
          ...prev,
          [id]: Array.isArray(response.data) ? response.data : [],
        }));
      })
      .catch(() => {
        toast.error("Failed to load notification history.");
      })
      .finally(() => {
        setLoadingNotificationsByOrderId((prev) => ({ ...prev, [id]: false }));
      });
  }, []);

  const getStatusLabel = (status) =>
    ORDER_STATUS_LABELS[status] ?? STATUS_STAGES[status] ?? "Unknown";

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-3xl font-bold">View Orders</h1>
        <div className="relative inline-block">
          <Button onClick={fetchOrders} color="blue">
            Refresh
          </Button>
          {newOrdersCount > 0 && (
            <span
              className="absolute -top-1 -right-1 min-w-[1.25rem] h-5 px-1 inline-flex items-center justify-center bg-red-500 text-white text-xs font-bold rounded-full"
              aria-label={`${newOrdersCount} new orders`}
            >
              {newOrdersCount > 99 ? "99+" : newOrdersCount}
            </span>
          )}
        </div>
      </div>
      {orders.length === 0 ? (
        <p className="text-gray-500">No orders available</p>
      ) : (
        <div className="space-y-4">
          {sortedOrders.map((order) => {
            const id = orderId(order);
            const status = orderStatusValue(order);
            const isComplete = status === ORDER_STATUS.Completed;
            const advanceLabel =
              status === ORDER_STATUS.ReadyForPickup
                ? "Mark complete"
                : "Advance status";
            const notifications = orderNotifications[id] ?? [];
            const loadingNotifications = Boolean(loadingNotificationsByOrderId[id]);

            return (
            <Card
              key={id}
              title={`Order #${id}`}
              className="mb-2"
            >
              <div className="flex items-center gap-2 mb-4 flex-wrap">
                <span
                  className={`px-2 py-1 rounded text-sm font-medium ${
                    isComplete
                      ? "bg-green-200 text-green-800"
                      : "bg-blue-100 text-blue-800"
                  }`}
                >
                  {getStatusLabel(status)}
                </span>
                {isComplete ? (
                  <span className="text-sm font-medium text-green-800">
                    Completed — no further action
                  </span>
                ) : (
                  <Button
                    onClick={() => advanceStatus(id)}
                    color="green"
                  >
                    {advanceLabel}
                  </Button>
                )}
                <Button
                  onClick={() => fetchOrderNotifications(id)}
                  color="gray"
                >
                  {loadingNotifications ? "Loading..." : "Refresh notifications"}
                </Button>
              </div>
              <p className="mb-1">
                <strong>Customer:</strong> {order.customerName ?? order.CustomerName}
              </p>
              {(order.customerPhone ?? order.CustomerPhone) && (
                <p className="mb-1">
                  <strong>Phone:</strong>{" "}
                  {order.customerPhone ?? order.CustomerPhone}
                </p>
              )}
              <p className="mb-4">
                <strong>Date:</strong>{" "}
                {new Date(order.orderDate ?? order.OrderDate).toLocaleString()}
              </p>

              <ul className="space-y-2">
                {(order.orderItems || order.OrderItems || []).map((item) => (
                  <li key={item.id} className="flex flex-col border-b pb-2">
                    {item.menuItem?.name ? (
                      <>
                        <span>
                          <strong>Item:</strong> {item.menuItem.name}
                        </span>
                        <span>
                          {" "}
                          <strong>Qty:</strong> {item.quantity}
                        </span>
                        {item.notes && (
                          <span>
                            {" "}
                            <strong>Notes:</strong> {item.notes}
                          </span>
                        )}
                      </>
                    ) : (
                      <span className="text-red-500">
                        Item data unavailable
                      </span>
                    )}
                  </li>
                ))}
              </ul>
              <div className="mt-4 pt-3 border-t border-gray-200">
                <p className="font-semibold mb-2">Notification delivery</p>
                {notifications.length === 0 ? (
                  <p className="text-sm text-gray-500">
                    No notification events loaded for this order yet.
                  </p>
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
