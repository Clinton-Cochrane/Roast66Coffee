import React, { useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import Header from "../components/layout/Header";
import Button from "../components/common/Button";
import ViewOrders from "../components/Admin/ViewOrders";
import StaffDevicePrompt from "../components/Admin/StaffDevicePrompt";
import { useI18n } from "../i18n/LanguageContext";

function CashPage() {
  const { t } = useI18n();
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
        <Header color="bg-blue-900" title={t("cash.dashboardTitle")} />

        <div className="flex flex-wrap gap-3">
          <Button color="green" onClick={handleNewOrder}>
            {t("cash.newOrder")}
          </Button>
          <Button color="gray" onClick={handleLogout}>
            {t("common.logOut")}
          </Button>
        </div>
        <StaffDevicePrompt />

        <div className="bg-white rounded-lg shadow border border-gray-200 p-6">
          <ViewOrders />
        </div>
      </div>
    </div>
  );
}

export default CashPage;
