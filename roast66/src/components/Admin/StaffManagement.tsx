import React, { useCallback, useEffect, useState, type FormEvent } from "react";
import axios from "axios";
import { toast } from "react-toastify";
import axiosInstance from "../../axiosConfig";
import { useI18n } from "../../i18n/LanguageContext";
import type { StaffAccountDto } from "../../types/api";
import Button from "../common/Button";
import FormInput from "../common/FormInput";

type StaffManagementProps = {
  currentUserId: string;
};

function StaffManagement({ currentUserId }: StaffManagementProps) {
  const { t } = useI18n();
  const [staff, setStaff] = useState<StaffAccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [displayName, setDisplayName] = useState("");
  const [username, setUsername] = useState("");
  const [initialPassword, setInitialPassword] = useState("");
  const [isOwner, setIsOwner] = useState(false);
  const [resetTargetId, setResetTargetId] = useState<string | null>(null);
  const [resetPassword, setResetPassword] = useState("");

  const loadStaff = useCallback(() => {
    setLoading(true);
    return axiosInstance
      .get<StaffAccountDto[]>("/admin/staff")
      .then((response) => setStaff(Array.isArray(response.data) ? response.data : []))
      .catch(() => toast.error(t("staff.loadFailed")))
      .finally(() => setLoading(false));
  }, [t]);

  useEffect(() => {
    void loadStaff();
  }, [loadStaff]);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    axiosInstance
      .post<StaffAccountDto>("/admin/staff", {
        displayName: displayName.trim(),
        username: username.trim(),
        initialPassword,
        isOwner,
      })
      .then(() => {
        toast.success(t("staff.created"));
        setDisplayName("");
        setUsername("");
        setInitialPassword("");
        setIsOwner(false);
        return loadStaff();
      })
      .catch((error: unknown) => {
        const data = axios.isAxiosError(error) ? error.response?.data : undefined;
        const message =
          data && typeof data === "object" && "message" in data
            ? String((data as { message?: string }).message)
            : t("staff.createFailed");
        toast.error(message);
      });
  };

  const setActive = (account: StaffAccountDto) => {
    const action = account.isActive ? "disable" : "enable";
    axiosInstance
      .post(`/admin/staff/${account.id}/${action}`)
      .then(() => {
        toast.success(account.isActive ? t("staff.disabled") : t("staff.enabled"));
        return loadStaff();
      })
      .catch(() => toast.error(t("staff.updateFailed")));
  };

  const submitReset = (event: FormEvent) => {
    event.preventDefault();
    if (!resetTargetId) return;
    axiosInstance
      .post(`/admin/staff/${resetTargetId}/reset-password`, {
        newPassword: resetPassword,
      })
      .then(() => {
        toast.success(t("staff.passwordReset"));
        setResetTargetId(null);
        setResetPassword("");
      })
      .catch(() => toast.error(t("staff.passwordResetFailed")));
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold">{t("staff.title")}</h2>
        <p className="text-gray-600">{t("staff.description")}</p>
      </div>

      <form onSubmit={submit} className="grid gap-3 rounded border border-gray-200 p-4 md:grid-cols-2">
        <h3 className="text-lg font-semibold md:col-span-2">{t("staff.addTitle")}</h3>
        <FormInput
          name="staff-display-name"
          label={t("staff.displayName")}
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
          required
        />
        <FormInput
          name="staff-username"
          label={t("staff.username")}
          value={username}
          onChange={(event) => setUsername(event.target.value)}
          required
        />
        <FormInput
          type="password"
          name="staff-initial-password"
          label={t("staff.initialPassword")}
          value={initialPassword}
          onChange={(event) => setInitialPassword(event.target.value)}
          minLength={12}
          required
        />
        <label className="flex items-center gap-2">
          <input type="checkbox" checked={isOwner} onChange={(event) => setIsOwner(event.target.checked)} />
          {t("staff.ownerAccess")}
        </label>
        <div className="md:col-span-2">
          <Button type="submit" color="green">{t("staff.create")}</Button>
        </div>
      </form>

      {loading ? (
        <p role="status">{t("staff.loading")}</p>
      ) : (
        <ul className="space-y-3">
          {staff.map((account) => (
            <li key={account.id} className="rounded border border-gray-200 p-4">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <strong>{account.displayName}</strong>
                  <p className="text-sm text-gray-600">
                    {account.username} · {account.roles.join(", ")} ·{" "}
                    {account.isActive ? t("staff.active") : t("staff.inactive")}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button
                    color={account.isActive ? "gray" : "green"}
                    onClick={() => setActive(account)}
                    disabled={account.id === currentUserId}
                  >
                    {account.isActive ? t("staff.disable") : t("staff.enable")}
                  </Button>
                  <Button color="blue" onClick={() => setResetTargetId(account.id)}>
                    {t("staff.resetPassword")}
                  </Button>
                </div>
              </div>
              {resetTargetId === account.id ? (
                <form onSubmit={submitReset} className="mt-3 flex flex-wrap items-end gap-2">
                  <FormInput
                    type="password"
                    name={`reset-password-${account.id}`}
                    label={t("staff.newPassword")}
                    value={resetPassword}
                    onChange={(event) => setResetPassword(event.target.value)}
                    minLength={12}
                    required
                  />
                  <Button type="submit" color="green">{t("staff.savePassword")}</Button>
                  <Button type="button" color="gray" onClick={() => setResetTargetId(null)}>
                    {t("staff.cancel")}
                  </Button>
                </form>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export default StaffManagement;
