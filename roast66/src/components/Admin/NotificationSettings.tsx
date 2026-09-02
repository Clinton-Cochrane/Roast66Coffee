import React, { useEffect, useState, type FormEvent } from "react";
import axios from "../../axiosConfig";
import { toast } from "react-toastify";
import { useI18n } from "../../i18n/LanguageContext";

const controlClasses = "mx-auto my-2.5 block w-4/5 max-w-[400px] p-2.5";
const helperTextClasses = "mx-auto my-2 w-4/5 max-w-[400px] text-[0.95rem] text-gray-600";

function NotificationSettings() {
  const { t } = useI18n();
  const [adminEmail, setAdminEmail] = useState("");
  const [baristaEmail, setBaristaEmail] = useState("");
  const [trailerEmail, setTrailerEmail] = useState("");

  useEffect(() => {
    fetchNotificationSettings();
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

  return (
    <div className="p-5">
      <h2>{t("notificationSettings.title")}</h2>
      <form onSubmit={handleSaveSettings}>
        <p className={helperTextClasses}>{t("notificationSettings.helperSms")}</p>
        <input
          type="email"
          placeholder={t("notificationSettings.placeholderAdmin")}
          value={adminEmail}
          onChange={(e) => setAdminEmail(e.target.value)}
          className={controlClasses}
        />
        <input
          type="email"
          placeholder={t("notificationSettings.placeholderBarista")}
          value={baristaEmail}
          onChange={(e) => setBaristaEmail(e.target.value)}
          className={controlClasses}
        />
        <input
          type="email"
          placeholder={t("notificationSettings.placeholderTrailer")}
          value={trailerEmail}
          onChange={(e) => setTrailerEmail(e.target.value)}
          className={controlClasses}
        />
        <button type="submit" className={controlClasses}>
          {t("notificationSettings.saveButton")}
        </button>
      </form>
    </div>
  );
}

export default NotificationSettings;
