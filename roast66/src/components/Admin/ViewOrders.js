// src/components/Admin/ViewOrders.jsx
import React, { useEffect, useState } from "react";
import axios from "../../axiosConfig";
import { toast } from "react-toastify";
import Card from "../common/Card";
import Button from "../common/Button";
import { ORDER_STATUS_LABELS } from "../../constants/orderStatus";

const STATUS_STAGES = ["Received", "Preparing", "ReadyForPickup", "Completed"];

function ViewOrders() {
  const [orders, setOrders] = useState([]);

  useEffect(() => {
    fetchOrders();
  }, []);

  const fetchOrders = () => {
    axios
      .get("/admin/orders")
      .then((response) => setOrders(response.data))
      .catch(() => toast.error("Failed to fetch orders"));
  };

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
    <div className="min-h-screen bg-gray-100 p-6">
      <h1 className="text-3xl font-bold mb-6">View Orders</h1>
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
