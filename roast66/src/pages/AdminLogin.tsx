import React, { useState, type FormEvent } from "react";
import axios from "axios";
import axiosInstance from "../axiosConfig";
import { toast } from "react-toastify";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";
import { useI18n } from "../i18n/LanguageContext";

type AdminLoginProps = {
  onLoginSuccess?: () => void;
};

const AdminLogin = ({ onLoginSuccess }: AdminLoginProps) => {
  const { t } = useI18n();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isForgotLoading, setIsForgotLoading] = useState(false);

  const handleLogin = async (e: FormEvent) => {
    e.preventDefault();
    try {
      const response = await axiosInstance.post<{ token: string }>("/admin/login", {
        username,
        password,
      });
      localStorage.setItem("token", response.data.token);
      onLoginSuccess?.();
    } catch (err: unknown) {
      const status = axios.isAxiosError(err) ? err.response?.status : undefined;
      setError(
        status !== undefined && status >= 500
          ? t("adminLogin.serverError")
          : t("adminLogin.invalidCredentials")
      );
    }
  };

  const handleForgotPassword = async () => {
    setIsForgotLoading(true);
    try {
      await axiosInstance.post("/admin/forgot-password", {});
      toast.success(t("adminLogin.forgotSent"));
    } catch (err: unknown) {
      const status = axios.isAxiosError(err) ? err.response?.status : undefined;
      const data = axios.isAxiosError(err) ? err.response?.data : undefined;
      const message =
        (data && typeof data === "object" && "message" in data
          ? String((data as { message?: string }).message)
          : null) ||
        (status === 503 ? t("adminLogin.forgotNotConfigured") : t("adminLogin.forgotFailed"));
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
          onClick={() => void handleForgotPassword()}
          disabled={isForgotLoading}
        >
          {isForgotLoading ? t("adminLogin.sending") : t("adminLogin.forgotPassword")}
        </Button>
        {error ? <p className="text-red-500">{error}</p> : null}
      </form>
    </div>
  );
};

export default AdminLogin;
