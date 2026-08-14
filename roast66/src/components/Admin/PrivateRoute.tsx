import React, { type ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { useAdminToken } from "../../authSession";

const PrivateRoute = ({ children }: { children: ReactNode }) => {
  const token = useAdminToken();
  return token ? children : <Navigate to="/admin-login" replace />;
};

export default PrivateRoute;
