import React, { useState, type FormEvent } from "react";
import axios from "axios";
import { toast } from "react-toastify";
import axiosInstance from "../../axiosConfig";
import { useI18n } from "../../i18n/LanguageContext";
import type { StaffAccountDto } from "../../types/api";
import Button from "../common/Button";
import FormInput from "../common/FormInput";

type AccountSecurityProps = {
  account: StaffAccountDto;
};

function AccountSecurity({ account }: AccountSecurityProps) {
  const { t } = useI18n();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [saving, setSaving] = useState(false);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    axiosInstance
      .post("/admin/me/change-password", { currentPassword, newPassword })
      .then(() => {
        setCurrentPassword("");
        setNewPassword("");
        toast.success(t("account.passwordChanged"));
      })
      .catch((error: unknown) => {
        const data = axios.isAxiosError(error) ? error.response?.data : undefined;
        const message =
          data && typeof data === "object" && "message" in data
            ? String((data as { message?: string }).message)
            : t("account.passwordChangeFailed");
        toast.error(message);
      })
      .finally(() => setSaving(false));
  };

  return (
    <div className="max-w-xl space-y-5">
      <div>
        <h2 className="text-2xl font-bold">{t("account.title")}</h2>
        <p className="text-gray-600">
          {account.displayName} · {account.username}
        </p>
      </div>
      <form onSubmit={submit} className="space-y-3 rounded border border-gray-200 p-4">
        <h3 className="text-lg font-semibold">{t("account.changePassword")}</h3>
        <FormInput
          type="password"
          name="current-password"
          label={t("account.currentPassword")}
          value={currentPassword}
          onChange={(event) => setCurrentPassword(event.target.value)}
          required
        />
        <FormInput
          type="password"
          name="new-password"
          label={t("account.newPassword")}
          value={newPassword}
          onChange={(event) => setNewPassword(event.target.value)}
          minLength={12}
          required
        />
        <p className="text-sm text-gray-600">{t("account.passwordHelp")}</p>
        <Button type="submit" color="green" disabled={saving}>
          {saving ? t("account.saving") : t("account.savePassword")}
        </Button>
      </form>
    </div>
  );
}

export default AccountSecurity;
