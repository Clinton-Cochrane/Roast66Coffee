import React, { useState } from "react";
import PropTypes from "prop-types";
import axios from "../axiosConfig";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";

const AdminLogin = ({ onLoginSuccess }) => {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  const handleLogin = async (e) => {
    e.preventDefault();
    try {
      const response = await axios.post("/admin/login", { username, password });
      localStorage.setItem("token", response.data.token);
      onLoginSuccess?.();
    } catch (err) {
      setError("Invalid credentials");
    }
  };

  return (
    <div className="p-6 max-w-sm mx-auto">
      <h2 className="text-2xl font-bold mb-4">Admin Login</h2>
      <form onSubmit={handleLogin} className="space-y-4">
        <FormInput
          type="text"
          placeholder="Username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          className="w-full p-2 border rounded"
        />
        <FormInput
          type="password"
          placeholder="Password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="w-full p-2 border rounded"
        />
        <Button type="submit" color="green" className="w-full">
          Login
        </Button>
        {error && <p className="text-red-500">{error}</p>}
      </form>
    </div>
  );
};

AdminLogin.propTypes = {
  onLoginSuccess: PropTypes.func,
};

AdminLogin.defaultProps = {
  onLoginSuccess: null,
};

export default AdminLogin;