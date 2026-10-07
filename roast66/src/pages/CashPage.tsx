import React, { useEffect, useCallback, useRef, useState, type KeyboardEvent } from "react";
import { useNavigate } from "react-router-dom";
import Header from "../components/layout/Header";
import Button from "../components/common/Button";
import ViewOrders from "../components/Admin/ViewOrders";
import StaffDevicePrompt from "../components/Admin/StaffDevicePrompt";
import { useI18n } from "../i18n/LanguageContext";
import { clearAdminSession, getAdminToken } from "../authSession";
import OrderPage from "./OrderPage";
import type { OrderDto } from "../types/api";

type CashTab = "newOrder" | "orders";

function CashPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<CashTab>("newOrder");
  const [completedOrder, setCompletedOrder] = useState<{
    order: OrderDto;
    wasReplay: boolean;
  } | null>(null);
  const confirmationHeadingRef = useRef<HTMLHeadingElement>(null);
  const newOrderPanelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const token = getAdminToken();
    if (!token) {
      navigate("/cash", { replace: true });
    }
  }, [navigate]);

  useEffect(() => {
    if (completedOrder && !newOrderPanelRef.current?.hidden) {
      confirmationHeadingRef.current?.focus();
    }
  }, [completedOrder]);

  const handleOrderCompleted = useCallback((order: OrderDto, wasReplay: boolean) => {
    setCompletedOrder({ order, wasReplay });
  }, []);

  const selectTab = (tab: CashTab) => {
    setActiveTab(tab);
    document.getElementById(`cash-tab-${tab}`)?.focus();
  };

  const handleTabKeyDown = (event: KeyboardEvent<HTMLButtonElement>, currentTab: CashTab) => {
    let nextTab: CashTab;
    switch (event.key) {
      case "ArrowLeft":
      case "ArrowRight":
        nextTab = currentTab === "newOrder" ? "orders" : "newOrder";
        break;
      case "Home":
        nextTab = "newOrder";
        break;
      case "End":
        nextTab = "orders";
        break;
      default:
        return;
    }
    event.preventDefault();
    selectTab(nextTab);
  };

  const handleLogout = useCallback(() => {
    clearAdminSession();
    navigate("/cash", { replace: true });
  }, [navigate]);

  return (
    <div className="min-h-screen bg-gray-100 p-3 sm:p-6">
      <div className="max-w-6xl mx-auto space-y-4 sm:space-y-6">
        <Header color="bg-blue-900" title={t("cash.dashboardTitle")} />

        <div className="flex justify-end">
          <Button color="gray" onClick={handleLogout}>
            {t("common.logOut")}
          </Button>
        </div>
        <StaffDevicePrompt />

        <div className="bg-white rounded-lg shadow border border-gray-200">
          <div
            role="tablist"
            aria-label={t("cash.tablistAriaLabel")}
            className="flex flex-wrap gap-0 rounded-t-lg border-b border-gray-200 bg-gray-50 px-2 pt-2"
          >
            {([
              { id: "newOrder", label: t("cash.newOrder") },
              { id: "orders", label: t("cash.orders") },
            ] as const).map(({ id, label }) => (
              <button
                key={id}
                type="button"
                role="tab"
                id={`cash-tab-${id}`}
                aria-selected={activeTab === id}
                aria-controls={`cash-panel-${id}`}
                tabIndex={activeTab === id ? 0 : -1}
                onClick={() => selectTab(id)}
                onKeyDown={(event) => handleTabKeyDown(event, id)}
                className={`px-4 py-3 text-sm font-medium rounded-t-md border border-b-0 transition-colors focus:outline-hidden focus-visible:ring-2 focus-visible:ring-blue-900 focus-visible:ring-offset-2 ${
                  activeTab === id
                    ? "bg-white text-blue-900 border-gray-200 relative z-10 mb-[-1px]"
                    : "bg-transparent text-gray-600 border-transparent hover:text-gray-900 hover:bg-gray-100"
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          {/* Keep both panels mounted so tab switches preserve drafts and Orders polling. */}
          <div
            id="cash-panel-newOrder"
            ref={newOrderPanelRef}
            role="tabpanel"
            aria-labelledby="cash-tab-newOrder"
            hidden={activeTab !== "newOrder"}
          >
            {completedOrder ? (
              <div className="p-4 sm:p-6 space-y-4">
                <h2 ref={confirmationHeadingRef} tabIndex={-1} className="text-2xl font-bold text-blue-900">
                  {t("cash.orderConfirmed")}
                </h2>
                <p className="text-lg font-semibold">
                  {t("cash.confirmedOrderNumber", {
                    orderId: completedOrder.order.id ?? completedOrder.order.Id ?? 0,
                  })}
                </p>
                <p>{completedOrder.order.customerName ?? completedOrder.order.CustomerName}</p>
                {completedOrder.wasReplay ? (
                  <p className="rounded border border-amber-200 bg-amber-50 p-3 text-amber-900">
                    {t("cash.alreadySubmitted")}
                  </p>
                ) : null}
                <div className="flex flex-wrap gap-3">
                  <Button color="green" onClick={() => {
                    setCompletedOrder(null);
                    selectTab("newOrder");
                  }}>
                    {t("cash.newOrder")}
                  </Button>
                  <Button color="blue" onClick={() => selectTab("orders")}>
                    {t("cash.orders")}
                  </Button>
                </div>
              </div>
            ) : (
              <OrderPage onOrderCompleted={handleOrderCompleted} isActive={activeTab === "newOrder"} />
            )}
          </div>

          <div
            id="cash-panel-orders"
            role="tabpanel"
            aria-labelledby="cash-tab-orders"
            hidden={activeTab !== "orders"}
            className="p-3 sm:p-6"
          >
            <ViewOrders />
          </div>
        </div>
      </div>
    </div>
  );
}

export default CashPage;
