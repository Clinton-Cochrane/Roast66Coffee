import React, { useState } from "react";
import axios from "../axiosConfig";
import { toast } from "react-toastify";
import OrderTracker from "../components/Customer/OrderTracker";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";

function OrderStatusPage() {
  const [orderId, setOrderId] = useState("");
  const [phone, setPhone] = useState("");
  const [order, setOrder] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleLookup = async (e) => {
    e.preventDefault();
    if (!orderId.trim() || !phone.trim()) {
      toast.error("Please enter both order ID and phone number.");
      return;
    }
    setIsLoading(true);
    setOrder(null);
    try {
      const { data } = await axios.get("/order/lookup", {
        params: { orderId: parseInt(orderId, 10), phone: phone.trim() },
      });
      setOrder(data);
    } catch (err) {
      if (err.response?.status === 404) {
        toast.error("Order not found or phone number doesn't match.");
      } else {
        toast.error("Failed to look up order.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="p-6 max-w-lg mx-auto">
      <h1 className="text-3xl font-bold mb-6">Order Status</h1>
      <p className="text-gray-600 mb-6">
        Enter your order ID and phone number to track your order.
      </p>

      <form onSubmit={handleLookup} className="space-y-4 mb-8">
        <FormInput
          type="text"
          placeholder="Order ID (e.g. 42)"
          value={orderId}
          onChange={(e) => setOrderId(e.target.value)}
        />
        <FormInput
          type="tel"
          placeholder="Phone number"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
        />
        <Button type="submit" color="green" disabled={isLoading}>
          {isLoading ? "Looking up…" : "Check Status"}
        </Button>
      </form>

      {order && (
        <div className="bg-gray-50 rounded-lg p-4">
          <p className="text-lg font-bold mb-2">Order #{order.id}</p>
          <p className="text-gray-600 mb-4">
            {order.customerName} • {new Date(order.orderDate).toLocaleString()}
          </p>
          <ul className="space-y-1 mb-4">
            {(order.OrderItems || []).map((item, i) => (
              <li key={i}>
                {item.quantity}x {item.menuItem?.name ?? "Item"}
              </li>
            ))}
          </ul>
          <h2 className="text-xl font-bold mb-4">Status</h2>
          <OrderTracker currentStatus={order.orderStatus ?? 0} />
        </div>
      )}
    </div>
  );
}

export default OrderStatusPage;
