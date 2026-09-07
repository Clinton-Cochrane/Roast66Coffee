import React, { useState, type FormEvent } from "react";
import axios from "axios";
import axiosInstance from "../axiosConfig";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";
import { useI18n } from "../i18n/LanguageContext";
import { setAdminSession } from "../authSession";

type AdminLoginProps = {
  onLoginSuccess?: () => void;
};

const AdminLogin = ({ onLoginSuccess }: AdminLoginProps) => {
  const { t } = useI18n();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  const handleLogin = async (e: FormEvent) => {
    e.preventDefault();
    try {
      const response = await axiosInstance.post<{ token: string }>("/admin/login", {
        username,
        password,
      });
      setAdminSession(response.data.token);
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
        {error ? <p className="text-red-500">{error}</p> : null}
      </form>
    </div>
  );
};

export default AdminLogin;
