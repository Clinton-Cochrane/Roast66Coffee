import React, { useState } from "react";
import AdminLogin from "../../pages/AdminLogin";
import CashPage from "../../pages/CashPage";

function CashGate() {
  const [token, setToken] = useState(() => localStorage.getItem("token"));

  const handleLoginSuccess = () => {
    setToken(localStorage.getItem("token"));
  };

  if (token) {
    return <CashPage />;
  }

  return <AdminLogin onLoginSuccess={handleLoginSuccess} />;
}

export default CashGate;
