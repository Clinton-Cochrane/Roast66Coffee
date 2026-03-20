import React from "react";
import { useLocation, Link } from "react-router-dom";
import Button from "../components/common/Button";

function DuplicateOrderPage() {
  const location = useLocation();
  const order = location.state?.order;
  const existingOrderId = location.state?.existingOrderId ?? order?.id;

  return (
    <div className="p-6 max-w-lg mx-auto text-center">
      {/* Cute coffee cup SVG */}
      <div className="mb-6 flex justify-center" aria-hidden="true">
        <svg
          width="120"
          height="140"
          viewBox="0 0 120 140"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
          className="drop-shadow-md"
        >
          {/* Steam wisps */}
          <path
            d="M45 20 Q48 8 52 20 Q56 8 60 20 Q64 8 68 20 Q72 8 76 20"
            stroke="#94a3b8"
            strokeWidth="2"
            strokeLinecap="round"
            fill="none"
            opacity="0.6"
          />
          {/* Cup body */}
          <path
            d="M30 35 L30 95 Q30 110 55 110 L75 110 Q100 110 100 95 L100 35 Z"
            fill="#8B4513"
            stroke="#6B3410"
            strokeWidth="2"
          />
          {/* Cup rim highlight */}
          <ellipse cx="65" cy="35" rx="35" ry="6" fill="#A0522D" />
          {/* Coffee inside */}
          <path
            d="M35 40 L35 90 Q35 100 55 100 L75 100 Q95 100 95 90 L95 40 Z"
            fill="#3E2723"
          />
          {/* Handle */}
          <path
            d="M100 55 Q125 55 125 75 Q125 95 100 95"
            stroke="#8B4513"
            strokeWidth="8"
            fill="none"
            strokeLinecap="round"
          />
          {/* Little heart on cup */}
          <path
            d="M55 65 Q50 55 45 60 Q42 63 45 68 Q50 73 55 68 Q60 63 57 60 Q55 55 55 65"
            fill="#e11d48"
            opacity="0.9"
          />
        </svg>
      </div>

      <h1 className="text-2xl font-bold text-gray-800 mb-2">
        Whoops! Double brew detected
      </h1>
      <p className="text-gray-600 mb-4">
        Looks like that order went through already — maybe a double-tap or a
        second click? No worries, your coffee&apos;s already in the queue.
      </p>
      <p className="text-gray-600 mb-6">
        We caught the duplicate so you don&apos;t end up with two of the same
        order. One cup (or two, if you meant it!) is on its way.
      </p>

      {existingOrderId && (
        <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 mb-6 text-left">
          <p className="font-semibold text-amber-900">
            Your order #{existingOrderId} is confirmed.
          </p>
          <p className="text-sm text-amber-800 mt-1">
            Use Order Status to track when it&apos;s ready for pickup.
          </p>
        </div>
      )}

      <div className="flex flex-col sm:flex-row gap-3 justify-center">
        <Link to="/order-status">
          <Button color="green">Check Order Status</Button>
        </Link>
        <Link to="/order">
          <Button color="blue">Place a New Order</Button>
        </Link>
      </div>
    </div>
  );
}

export default DuplicateOrderPage;
