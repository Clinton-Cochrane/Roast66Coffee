import React, { useState } from "react";
import PropTypes from "prop-types";
import axios from "../axiosConfig";
import { toast } from "react-toastify";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";

const AdminLogin = ({ onLoginSuccess }) => {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isForgotLoading, setIsForgotLoading] = useState(false);

  const handleLogin = async (e) => {
    e.preventDefault();
    try {
      const response = await axios.post("/admin/login", { username, password });
      localStorage.setItem("token", response.data.token);
      onLoginSuccess?.();
    } catch (err) {
      const status = err.response?.status;
      setError(
        status >= 500
          ? "Server error. Please try again later or contact support."
          : "Invalid credentials"
      );
    }
  };

  const handleForgotPassword = async () => {
    setIsForgotLoading(true);
    try {
      await axios.post("/admin/forgot-password", {});
      toast.success("Support request sent. Check with the family tech contact.");
    } catch (err) {
      const status = err.response?.status;
      const message =
        err.response?.data?.message ||
        (status === 503
          ? "Password support is not configured right now."
          : "Could not send request. Please try again.");
      toast.error(message);
    } finally {
      setIsForgotLoading(false);
    }
  };

  return (
    <div className="p-6 max-w-sm mx-auto">
      <h2 className="text-2xl font-bold mb-4">Admin Login</h2>
      <form onSubmit={handleLogin} className="space-y-4">
        <FormInput
          type="text"
          name="username"
          placeholder="Username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          className="w-full p-2 border rounded"
        />
        <FormInput
          type="password"
          name="password"
          placeholder="Password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="w-full p-2 border rounded"
        />
        <Button type="submit" color="green" className="w-full">
          Login
        </Button>
        <Button
          type="button"
          color="gray"
          className="w-full"
          onClick={handleForgotPassword}
          disabled={isForgotLoading}
        >
          {isForgotLoading ? "Sending..." : "Forgot password?"}
        </Button>
        {error && <p className="text-red-500">{error}</p>}
      </form>
    </div>
  );
};

AdminLogin.propTypes = {
  onLoginSuccess: PropTypes.func,
};

export default AdminLogin;