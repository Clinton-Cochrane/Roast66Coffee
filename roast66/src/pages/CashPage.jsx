import React, { useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import Header from "../components/layout/Header";
import Button from "../components/common/Button";
import ViewOrders from "../components/Admin/ViewOrders";

function CashPage() {
  const navigate = useNavigate();

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      navigate("/cash", { replace: true });
    }
  }, [navigate]);

  const handleNewOrder = useCallback(() => {
    navigate("/order");
  }, [navigate]);

  const handleLogout = useCallback(() => {
    localStorage.removeItem("token");
    navigate("/cash", { replace: true });
  }, [navigate]);

  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <div className="max-w-6xl mx-auto space-y-6">
        <Header color="bg-blue-900" title="Counter Dashboard" />

        <div className="flex flex-wrap gap-3">
          <Button color="green" onClick={handleNewOrder}>
            New Order
          </Button>
          <Button color="gray" onClick={handleLogout}>
            Log out
          </Button>
        </div>

        <div className="bg-white rounded-lg shadow border border-gray-200 p-6">
          <ViewOrders />
        </div>
      </div>
    </div>
  );
}

export default CashPage;
