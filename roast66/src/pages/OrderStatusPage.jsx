import React, { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import axios from "../axiosConfig";
import { toast } from "react-toastify";
import OrderTracker from "../components/Customer/OrderTracker";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";

const ENABLE_STRIPE_PREPAY =
  process.env.REACT_APP_ENABLE_STRIPE_CHECKOUT === "true";

const STRIPE_PREPAY_RETURN_KEY = "stripePrepayReturn";

function OrderStatusPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [orderId, setOrderId] = useState("");
  const [phone, setPhone] = useState("");
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
        if (parsed.phone) setPhone(parsed.phone);
        if (parsed.orderId != null) setOrderId(String(parsed.orderId));
      } catch {
        /* ignore */
      }
      sessionStorage.removeItem(STRIPE_PREPAY_RETURN_KEY);
    }

    if (checkout === "success") {
      toast.success("Payment received. Thank you!");
    } else {
      toast.info("Payment was cancelled.");
    }

    const next = new URLSearchParams(searchParams);
    next.delete("checkout");
    next.delete("orderId");
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);

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

  const handlePrepay = async () => {
    if (!order) return;
    const idempotencyKey =
      window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`;
    sessionStorage.setItem(
      STRIPE_PREPAY_RETURN_KEY,
      JSON.stringify({ phone: phone.trim(), orderId: order.id })
    );
    setPrepayLoading(true);
    try {
      const { data } = await axios.post(
        "/payments/checkout-session",
        {
          existingOrderId: order.id,
          customerName: order.customerName,
          customerPhone: phone.trim(),
          orderItems: [],
        },
        { headers: { "X-Idempotency-Key": idempotencyKey } }
      );
      const checkoutUrl = data?.checkoutUrl;
      if (!checkoutUrl) {
        throw new Error("Missing checkout URL");
      }
      window.location.assign(checkoutUrl);
    } catch (err) {
      sessionStorage.removeItem(STRIPE_PREPAY_RETURN_KEY);
      const msg =
        err.response?.data?.message ||
        (err.response?.status === 503
          ? "Online payment is not available right now."
          : "Unable to start payment. Please try again.");
      toast.error(msg);
    } finally {
      setPrepayLoading(false);
    }
  };

  const isPaid = Boolean(order?.paidUtc ?? order?.PaidUtc);

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
          {isPaid && (
            <p className="text-green-700 font-semibold mb-4">
              Paid online — thank you!
            </p>
          )}
          <ul className="space-y-1 mb-4">
            {(order.orderItems ?? order.OrderItems ?? []).map((item, i) => (
              <li key={i}>
                {item.quantity}x {item.menuItem?.name ?? "Item"}
              </li>
            ))}
          </ul>
          <h2 className="text-xl font-bold mb-4">Status</h2>
          <OrderTracker currentStatus={order.orderStatus ?? 0} />

          {ENABLE_STRIPE_PREPAY && !isPaid && (
            <div className="mt-6 pt-4 border-t border-gray-200">
              <p className="text-gray-600 text-sm mb-3">
                Pay ahead with a card so pickup is quick — you&apos;ll be sent
                to our secure checkout (Stripe).
              </p>
              <Button
                type="button"
                color="blue"
                onClick={handlePrepay}
                disabled={prepayLoading}
              >
                {prepayLoading ? "Redirecting…" : "Prepay with card (Stripe)"}
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default OrderStatusPage;
