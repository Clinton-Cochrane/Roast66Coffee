import React from "react";
import { useLocation, useNavigate, Link } from "react-router-dom";
import OrderTracker from "../components/Customer/OrderTracker";
import Button from "../components/common/Button";

function OrderConfirmationPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const order = location.state?.order;

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

  const total = (order.OrderItems || []).reduce(
    (sum, item) =>
      sum +
      (item.menuItem?.price ?? 0) * (item.quantity ?? 1) +
      (item.addOns || []).reduce(
        (aSum, addOn) =>
          aSum + (addOn.menuItem?.price ?? 0) * (addOn.quantity ?? 1),
        0
      ),
    0
  );

  return (
    <div className="p-6 max-w-lg mx-auto">
      <h1 className="text-3xl font-bold mb-2">Order Confirmed!</h1>
      <p className="text-gray-600 mb-6">
        Thanks for your order, {order.customerName}. We&apos;ll have it ready
        soon.
      </p>

      <div className="bg-gray-50 rounded-lg p-4 mb-6">
        <p className="text-lg font-bold mb-2">Order #{order.id}</p>
        <ul className="space-y-1 mb-4">
          {(order.OrderItems || []).map((item, i) => (
            <li key={i}>
              {item.quantity}x {item.menuItem?.name ?? "Item"}
              {(item.addOns || []).length > 0 &&
                ` + ${item.addOns.map((a) => a.menuItem?.name).filter(Boolean).join(", ")}`}
              {item.notes && ` (${item.notes})`}
            </li>
          ))}
        </ul>
        <p className="font-bold">Total: ${total.toFixed(2)}</p>
      </div>

      <h2 className="text-xl font-bold mb-4">Order Status</h2>
      <OrderTracker currentStatus={order.orderStatus ?? 0} />

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
