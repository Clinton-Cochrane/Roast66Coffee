import React, { useState } from "react";
import PropTypes from "prop-types";
import axios from "../axiosConfig";
import { toast } from "react-toastify";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";
import { useI18n } from "../i18n/LanguageContext";

const AdminLogin = ({ onLoginSuccess }) => {
  const { t } = useI18n();
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
          ? t("adminLogin.serverError")
          : t("adminLogin.invalidCredentials")
      );
    }
  };

  const handleForgotPassword = async () => {
    setIsForgotLoading(true);
    try {
      await axios.post("/admin/forgot-password", {});
      toast.success(t("adminLogin.forgotSent"));
    } catch (err) {
      const status = err.response?.status;
      const message =
        err.response?.data?.message ||
        (status === 503
          ? t("adminLogin.forgotNotConfigured")
          : t("adminLogin.forgotFailed"));
      toast.error(message);
    } finally {
      setIsForgotLoading(false);
    }
  };

  return (
    <div className="p-6 max-w-sm mx-auto">
      <h2 className="text-2xl font-bold mb-4">{t("adminLogin.title")}</h2>
      <form onSubmit={handleLogin} className="space-y-4">
        <FormInput
          type="text"
          name="username"
          label={t("adminLogin.usernamePlaceholder")}
          placeholder={t("adminLogin.usernamePlaceholder")}
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          className="w-full p-2 border rounded"
        />
        <FormInput
          type="password"
          name="password"
          label={t("adminLogin.passwordPlaceholder")}
          placeholder={t("adminLogin.passwordPlaceholder")}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="w-full p-2 border rounded"
        />
        <Button type="submit" color="green" className="w-full">
          {t("adminLogin.login")}
        </Button>
        <Button
          type="button"
          color="gray"
          className="w-full"
          onClick={handleForgotPassword}
          disabled={isForgotLoading}
        >
          {isForgotLoading ? t("adminLogin.sending") : t("adminLogin.forgotPassword")}
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