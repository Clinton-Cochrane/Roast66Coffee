import React, { useState } from "react";
import AdminLogin from "../../pages/AdminLogin";
import AdminPage from "../../pages/AdminPage";

/**
 * Renders the admin area at /admin. Shows login form when unauthenticated,
 * admin dashboard when authenticated. No redirect—single URL for admin access.
 */
function AdminGate() {
  const [token, setToken] = useState(() => localStorage.getItem("token"));

  const handleLoginSuccess = () => {
    setToken(localStorage.getItem("token"));
  };

  if (token) {
    return <AdminPage />;
  }

  return <AdminLogin onLoginSuccess={handleLoginSuccess} />;
}

export default AdminGate;
