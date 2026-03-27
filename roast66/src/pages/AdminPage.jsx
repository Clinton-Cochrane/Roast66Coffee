// src/pages/AdminPage.jsx
import React, { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import ManageMenu from "../components/Admin/ManageMenu";
import MenuBulkOperations from "../components/Admin/MenuBulkOperations";
import ViewOrders from "../components/Admin/ViewOrders";
import Header from "../components/layout/Header";
import "../styles/AdminPage.css";
import NotificationSettings from "../components/Admin/NotificationSettings";
import Loading from "../components/common/Loading";
import Button from "../components/common/Button";
import useKeepAliveHeartbeat from "../hooks/useKeepAliveHeartbeat";

const ADMIN_TAB_IDS = {
  ORDERS: "orders",
  MENU: "menu",
  SETTINGS: "settings",
};

const ADMIN_TABS = Object.freeze([
  { id: ADMIN_TAB_IDS.ORDERS, label: "Orders" },
  { id: ADMIN_TAB_IDS.MENU, label: "Menu management" },
  { id: ADMIN_TAB_IDS.SETTINGS, label: "Settings" },
]);

function AdminPage() {
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(true);
  const [menuRefreshKey, setMenuRefreshKey] = useState(0);
  const [activeTab, setActiveTab] = useState(ADMIN_TAB_IDS.ORDERS);
  useKeepAliveHeartbeat(true);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      navigate("/admin-login");
    } else {
      setIsLoading(false);
    }
  }, [navigate]);

  const handleMenuUpdated = useCallback(() => {
    setMenuRefreshKey((k) => k + 1);
  }, []);

  const handleLogout = useCallback(() => {
    localStorage.removeItem("token");
    navigate("/admin", { replace: true });
  }, [navigate]);

  if (isLoading) {
    return <Loading />;
  }

  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <div className="max-w-6xl mx-auto space-y-6">
        <Header color="bg-red-800" title="Admin Dashboard" />
        <div className="flex justify-end">
          <Button color="gray" onClick={handleLogout}>
            Log out
          </Button>
        </div>

        <div
          className="bg-white rounded-lg shadow border border-gray-200 overflow-hidden"
          data-testid="admin-console"
        >
          <div
            role="tablist"
            aria-label="Admin sections"
            className="flex flex-wrap gap-0 border-b border-gray-200 bg-gray-50 px-2 pt-2"
          >
            {ADMIN_TABS.map(({ id, label }) => {
              const selected = activeTab === id;
              return (
                <button
                  key={id}
                  type="button"
                  role="tab"
                  id={`admin-tab-${id}`}
                  aria-selected={selected}
                  aria-controls={`admin-panel-${id}`}
                  onClick={() => setActiveTab(id)}
                  className={`px-4 py-3 text-sm font-medium rounded-t-md border border-b-0 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-red-800 focus-visible:ring-offset-2 ${
                    selected
                      ? "bg-white text-red-900 border-gray-200 relative z-10 mb-[-1px]"
                      : "bg-transparent text-gray-600 border-transparent hover:text-gray-900 hover:bg-gray-100"
                  }`}
                >
                  {label}
                </button>
              );
            })}
          </div>

          <div className="p-6">
            {activeTab === ADMIN_TAB_IDS.ORDERS && (
              <div
                id={`admin-panel-${ADMIN_TAB_IDS.ORDERS}`}
                role="tabpanel"
                aria-labelledby={`admin-tab-${ADMIN_TAB_IDS.ORDERS}`}
              >
                <ViewOrders />
              </div>
            )}

            {activeTab === ADMIN_TAB_IDS.MENU && (
              <div
                id={`admin-panel-${ADMIN_TAB_IDS.MENU}`}
                role="tabpanel"
                aria-labelledby={`admin-tab-${ADMIN_TAB_IDS.MENU}`}
                className="space-y-6"
              >
                <MenuBulkOperations onMenuUpdated={handleMenuUpdated} />
                <ManageMenu key={menuRefreshKey} />
              </div>
            )}

            {activeTab === ADMIN_TAB_IDS.SETTINGS && (
              <div
                id={`admin-panel-${ADMIN_TAB_IDS.SETTINGS}`}
                role="tabpanel"
                aria-labelledby={`admin-tab-${ADMIN_TAB_IDS.SETTINGS}`}
              >
                <NotificationSettings />
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export default AdminPage;
