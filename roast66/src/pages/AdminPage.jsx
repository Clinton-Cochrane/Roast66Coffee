// src/pages/AdminPage.js
import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import ManageMenu from "../components/Admin/ManageMenu";
import ViewOrders from "../components/Admin/ViewOrders";
import Header from "../components/layout/Header";
import "../styles/AdminPage.css";
import NotificationSettings from "../components/Admin/NotificationSettings";
import Loading from "../components/common/Loading";

function AdminPage() {
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      navigate("/admin-login");
    } else {
      setIsLoading(false);
    }
  }, [navigate]);

  if (isLoading) {
    return <Loading />;
  }

  return (
    <div className="min-h-screen bg-gray-100 p-6 space-y-6">
      <Header color = "bg-red-800" title ="Admin Dashboard"/>
      <ManageMenu />
      <ViewOrders />
      <NotificationSettings />
    </div>
  );
}

export default AdminPage;
