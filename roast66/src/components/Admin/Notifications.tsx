import React from "react";
import { useI18n } from "../../i18n/LanguageContext";

function Notifications() {
  const { t } = useI18n();

  return (
    <div className="notifications">
      <h2>{t("admin.notificationsTitle")}</h2>
      <p>{t("admin.notificationsPlaceholder")}</p>
    </div>
  );
}

export default Notifications;
