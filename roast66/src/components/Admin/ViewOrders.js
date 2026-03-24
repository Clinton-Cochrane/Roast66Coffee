// src/components/Admin/ViewOrders.jsx
import React, { useCallback, useEffect, useState } from "react";
import axios from "../../axiosConfig";
import { toast } from "react-toastify";
import Card from "../common/Card";
import Button from "../common/Button";
import { ORDER_STATUS_LABELS } from "../../constants/orderStatus";

const STATUS_STAGES = ["Received", "Preparing", "ReadyForPickup", "Completed"];
const POLL_INTERVAL_MS = 60000;

function ViewOrders() {
  const [orders, setOrders] = useState([]);
  const [lastRefreshedAt, setLastRefreshedAt] = useState(null);
  const [newOrdersCount, setNewOrdersCount] = useState(0);

  const fetchOrders = useCallback(() => {
    axios
      .get("/admin/orders")
      .then((response) => {
        setOrders(response.data);
        setLastRefreshedAt(new Date());
        setNewOrdersCount(0);
      })
      .catch(() => toast.error("Failed to fetch orders"));
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

  const advanceStatus = (orderId) => {
    axios
      .put(`/admin/updateOrderStatus/${orderId}/status`)
      .then(() => {
        toast.success("Order status updated.");
        fetchOrders();
      })
      .catch(() => toast.error("Failed to update status"));
  };

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
          {orders.map((order) => (
            <Card
              key={order.id}
              title={`Order #${order.id}`}
              className="mb-2"
            >
              <div className="flex items-center gap-2 mb-4">
                <span
                  className={`px-2 py-1 rounded text-sm font-medium ${
                    order.orderStatus === 3
                      ? "bg-green-200 text-green-800"
                      : "bg-blue-100 text-blue-800"
                  }`}
                >
                  {getStatusLabel(order.orderStatus)}
                </span>
                <Button
                  onClick={() => advanceStatus(order.id)}
                  color="green"
                  disabled={order.orderStatus === 3}
                >
                  {order.orderStatus === 3 ? "Complete" : "Advance status"}
                </Button>
              </div>
              <p className="mb-1">
                <strong>Customer:</strong> {order.customerName}
              </p>
              {order.customerPhone && (
                <p className="mb-1">
                  <strong>Phone:</strong> {order.customerPhone}
                </p>
              )}
              <p className="mb-4">
                <strong>Date:</strong>{" "}
                {new Date(order.orderDate).toLocaleString()}
              </p>

              <ul className="space-y-2">
                {(order.OrderItems || []).map((item) => (
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
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

export default ViewOrders;
