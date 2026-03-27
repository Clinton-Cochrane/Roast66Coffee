import React from "react";
import { useLocation, useNavigate, Link } from "react-router-dom";
import OrderTracker from "../components/Customer/OrderTracker";
import Button from "../components/common/Button";

function OrderConfirmationPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const order = location.state?.order;
  const customerEmail = order?.customerEmail ?? order?.CustomerEmail ?? "";
  const emailOptIn = Boolean(order?.customerNotificationOptIn ?? order?.CustomerNotificationOptIn);

  if (!order) {
    return (
      <div className="p-6 max-w-lg mx-auto">
        <h1 className="text-2xl font-bold mb-4">View Order Status</h1>
        <p className="text-gray-600 mb-4">
          Your order was placed. To check status later, go to{" "}
          <Link to="/order-status" className="text-green-700 underline">
            Order Status
          </Link>{" "}
          and enter your order ID and phone number.
        </p>
        <Button onClick={() => navigate("/order-status")} color="green">
          Check Order Status
        </Button>
      </div>
    );
  }

  const items = order.orderItems ?? order.OrderItems ?? [];
  const total = items.reduce(
    (sum, item) =>
      sum +
      (item.menuItem?.price ?? 0) * (item.quantity ?? 1) +
      (item.addOns ?? []).reduce(
        (aSum, addOn) =>
          aSum + (addOn.menuItem?.price ?? 0) * (addOn.quantity ?? 1),
        0
      ),
    0
  );

  const handleDownloadSummary = () => {
    const summary = {
      orderNumber: order.id ?? order.Id,
      customerName: order.customerName ?? order.CustomerName,
      customerPhone: order.customerPhone ?? order.CustomerPhone,
      trackerUrl: `${window.location.origin}/order-status`,
      items: items.map((item) => ({
        name: item.menuItem?.name ?? "Item",
        quantity: item.quantity ?? 1,
        addOns: (item.addOns ?? []).map((a) => ({
          name: a.menuItem?.name ?? "Add-on",
          quantity: a.quantity ?? 1,
        })),
        notes: item.notes ?? "",
      })),
      total,
    };

    const blob = new Blob([JSON.stringify(summary, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `roast66-order-${order.id ?? order.Id}.json`;
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="p-6 max-w-lg mx-auto">
      <h1 className="text-3xl font-bold mb-2">Order Confirmed!</h1>
        <p className="text-gray-600 mb-6">
        Thanks for your order, {order.customerName ?? order.CustomerName}. We&apos;ll have it ready
        soon.
      </p>

      <div className="bg-gray-50 rounded-lg p-4 mb-6">
        <p className="text-lg font-bold mb-2">Order #{order.id}</p>
        <ul className="space-y-1 mb-4">
          {items.map((item, i) => (
            <li key={i}>
              {item.quantity}x {item.menuItem?.name ?? "Item"}
              {(item.addOns || []).length > 0 &&
                ` + ${item.addOns.map((a) => a.menuItem?.name).filter(Boolean).join(", ")}`}
              {item.notes && ` (${item.notes})`}
            </li>
          ))}
        </ul>
        <p className="font-bold">Total: ${total.toFixed(2)}</p>
        {emailOptIn && customerEmail ? (
          <p className="text-sm text-gray-600 mt-2">
            We will send order status updates to <strong>{customerEmail}</strong>.
          </p>
        ) : (
          <div className="mt-3">
            <p className="text-sm text-gray-600 mb-2">
              Email updates are optional. Download your order summary for your records.
            </p>
            <Button color="gray" onClick={handleDownloadSummary}>
              Download Order Summary
            </Button>
          </div>
        )}
      </div>

      <h2 className="text-xl font-bold mb-4">Order Status</h2>
      <OrderTracker currentStatus={order.orderStatus ?? order.OrderStatus ?? 0} />

      <div className="mt-8 pt-6 border-t">
        <p className="text-sm text-gray-600 mb-2">
          Want to check status later? Go to{" "}
          <Link to="/order-status" className="text-green-700 underline">
            Order Status
          </Link>{" "}
          and enter order #{order.id} with your phone number.
        </p>
        <Link to="/menu">
          <Button color="blue">Back to Menu</Button>
        </Link>
      </div>
    </div>
  );
}

export default OrderConfirmationPage;
