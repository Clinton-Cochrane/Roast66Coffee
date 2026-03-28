import React, { useEffect, useState, type FormEvent } from "react";
import axios from "../../axiosConfig";
import { toast } from "react-toastify";
import "./NotificationSettings.css";
import { useI18n } from "../../i18n/LanguageContext";

type CredentialInfo = {
  username?: string;
  usernameEnvKey?: string;
  passwordEnvKey?: string;
};

function NotificationSettings() {
  const { t } = useI18n();
  const [adminEmail, setAdminEmail] = useState("");
  const [baristaEmail, setBaristaEmail] = useState("");
  const [trailerEmail, setTrailerEmail] = useState("");
  const [credentialInfo, setCredentialInfo] = useState<CredentialInfo | null>(null);
  const [credentialRequestMessage, setCredentialRequestMessage] = useState("");
  const [isSubmittingCredentialRequest, setIsSubmittingCredentialRequest] = useState(false);

  useEffect(() => {
    fetchNotificationSettings();
    fetchCredentialSettingsInfo();
  }, []);

  const fetchNotificationSettings = () => {
    axios
      .get("/admin/notificationSettings")
      .then((response) => {
        const data = response.data as Record<string, string>;
        setAdminEmail(data.adminEmail ?? "");
        setBaristaEmail(data.baristaEmail ?? "");
        setTrailerEmail(data.trailerEmail ?? "");
      })
      .catch((error: unknown) => console.error(error));
  };

  const fetchCredentialSettingsInfo = () => {
    axios
      .get("/admin/credential-settings")
      .then((response) => {
        setCredentialInfo((response.data as CredentialInfo) ?? null);
      })
      .catch((error: unknown) => console.error(error));
  };

  const handleSaveSettings = (e: FormEvent) => {
    e.preventDefault();
    axios
      .put("/admin/notificationSettings", {
        adminEmail: adminEmail.trim(),
        baristaEmail: baristaEmail.trim(),
        trailerEmail: trailerEmail.trim(),
      })
      .then(() => toast.success(t("notificationSettings.settingsSaved")))
      .catch((error: unknown) => console.error(error));
  };

  const handleCredentialRequest = (e: FormEvent) => {
    e.preventDefault();
    setIsSubmittingCredentialRequest(true);
    axios
      .post("/admin/forgot-password", {
        message: credentialRequestMessage.trim(),
      })
      .then(() => {
        toast.success(t("notificationSettings.credentialRequestSent"));
        setCredentialRequestMessage("");
      })
      .catch((error: unknown) => console.error(error))
      .finally(() => setIsSubmittingCredentialRequest(false));
  };

  const usernameKey = credentialInfo?.usernameEnvKey ?? t("notificationSettings.defaultUsernameKey");
  const passwordKey = credentialInfo?.passwordEnvKey ?? t("notificationSettings.defaultPasswordKey");

  return (
    <div className="notification-settings">
      <h2>{t("notificationSettings.title")}</h2>
      <form onSubmit={handleSaveSettings}>
        <p className="helper-text">{t("notificationSettings.helperSms")}</p>
        <input
          type="email"
          placeholder={t("notificationSettings.placeholderAdmin")}
          value={adminEmail}
          onChange={(e) => setAdminEmail(e.target.value)}
        />
        <input
          type="email"
          placeholder={t("notificationSettings.placeholderBarista")}
          value={baristaEmail}
          onChange={(e) => setBaristaEmail(e.target.value)}
        />
        <input
          type="email"
          placeholder={t("notificationSettings.placeholderTrailer")}
          value={trailerEmail}
          onChange={(e) => setTrailerEmail(e.target.value)}
        />
        <button type="submit">{t("notificationSettings.saveButton")}</button>
      </form>
      <section className="credentials-settings">
        <h2>{t("notificationSettings.credentialsTitle")}</h2>
        <p className="helper-text">{t("notificationSettings.credentialsHelper")}</p>
        <p className="helper-text">
          {t("notificationSettings.currentUsername")}{" "}
          <strong>{credentialInfo?.username ?? t("notificationSettings.unavailable")}</strong>
        </p>
        <p className="helper-text">
          {t("notificationSettings.updateEnvVars", { usernameKey, passwordKey })}
        </p>
        <form onSubmit={handleCredentialRequest}>
          <textarea
            placeholder={t("notificationSettings.credentialMessagePlaceholder")}
            value={credentialRequestMessage}
            onChange={(e) => setCredentialRequestMessage(e.target.value)}
            rows={3}
          />
          <button type="submit" disabled={isSubmittingCredentialRequest}>
            {isSubmittingCredentialRequest
              ? t("notificationSettings.sending")
              : t("notificationSettings.requestCredentialUpdate")}
          </button>
        </form>
      </section>
    </div>
  );
}

export default NotificationSettings;
