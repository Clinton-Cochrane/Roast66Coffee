import React, { useState } from "react";
import axios from "../axiosConfig";
import { useNavigate } from "react-router-dom";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";

const AdminLogin = () => {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const navigate = useNavigate();

  const handleLogin = async (e) => {
    e.preventDefault();
    try {
      const response = await axios.post("/Admin/login", { username, password });
      localStorage.setItem("token", response.data.token);
      navigate("/admin");
    // eslint-disable-next-line no-unused-vars
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

export default AdminLogin;