import React from "react";
import AdminLogin from "../../pages/AdminLogin";
import CashPage from "../../pages/CashPage";
import { setAdminSession, useAdminToken } from "../../authSession";

function CashGate() {
  const token = useAdminToken();

  if (token) {
    return <CashPage />;
  }

  return (
    <AdminLogin
      onLoginSuccess={() => {
        const nextToken = localStorage.getItem("token");
        if (nextToken) setAdminSession(nextToken);
      }}
    />
  );
}

export default CashGate;
