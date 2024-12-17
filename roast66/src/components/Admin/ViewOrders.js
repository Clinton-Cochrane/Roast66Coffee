// src/components/Admin/ViewOrders.jsx
import React, { useEffect, useState } from "react";
import axios from "../../axiosConfig";
import Card from "../common/Card";
import Button from "../common/Button";

function ViewOrders() {
  const [orders, setOrders] = useState([]);
  const [successMessage, setSuccessMessage] = useState("");

  useEffect(() => {
    fetchOrders();
  }, []);

  useEffect(() => {
    if (successMessage) {
      const timer = setTimeout(() => setSuccessMessage(""), 3000);
      return () => clearTimeout(timer);
    }
  }, [successMessage]);
  

  const fetchOrders = () => {
    axios
      .get("/admin/orders")
      .then((response) => setOrders(response.data))
      .catch((error) => console.error(error));
  };

  const completeOrder = (orderId) => {
    if (
      window.confirm("Are you sure you want to mark this order as complete?")
    ) {
      axios
        .put(`/admin/updateOrderStatus/${orderId}/status`)
        .then(() => {
          setSuccessMessage("Order status updated successfully.");
          fetchOrders();
        })
        .catch((err) => console.error(err));
    }
  };

  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <h1 className="text-3xl font-bold mb-6">View Orders</h1>
      {successMessage && <p className="text-green-500 mb-4">{successMessage}</p>}
      {orders.length === 0 ? (
        <p className="text-gray-500">No orders available</p>
      ) : (
        <div className="space-y-4">
          {orders.map((order) => (
            <Card
              key={order.id}
              title={`Order #${order.id}`}
              className={`mb-2 ${order.status ? "" : "text-gray-500"}`}
            >
              <Button onClick={() => completeOrder(order.id)} color="red">
                {order.status ? "Mark as Incomplete" : "Mark Order As Complete"}
              </Button>
              <p className="mb-2">
                <strong>Customer:</strong> {order.customerName}
              </p>
              <p className="mb-4">
                <strong>Date:</strong>{" "}
                {new Date(order.orderDate).toLocaleString()}
              </p>

              <ul className="space-y-2">
                {(order.OrderItems || []).map((item) => (
                  <li key={item.id} className="flex flex-col border-b pb-2">
                    {item.menuItem && item.menuItem.name ? (
                      <>
                        <span>
                          <strong>Item:</strong> {item.menuItem.name}
                        </span>
                        <span>
                          <strong>Quantity:</strong> {item.quantity}
                        </span>
                        {item.notes && (
                          <span>
                            {" "}
                            <strong>Notes: </strong>
                            {item.notes}
                          </span>
                        )}
                      </>
                    ) : (
                      <span className="text-red-500">
                        Item data is unavailable
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
