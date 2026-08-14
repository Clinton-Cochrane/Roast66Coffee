import React from "react";
import AdminLogin from "../../pages/AdminLogin";
import AdminPage from "../../pages/AdminPage";
import { setAdminSession, useAdminToken } from "../../authSession";

/**
 * Renders the admin area at /admin. Shows login form when unauthenticated,
 * admin dashboard when authenticated. No redirect—single URL for admin access.
 */
function AdminGate() {
  const token = useAdminToken();

  if (token) {
    return <AdminPage />;
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

export default AdminGate;
