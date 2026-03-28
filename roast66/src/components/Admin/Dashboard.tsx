import React from "react";
import Header from "../layout/Header";
import Card from "../common/Card";
import { useI18n } from "../../i18n/LanguageContext";

function Dashboard() {
  const { t } = useI18n();

  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <Header title={t("admin.dashboardTitle")} />
      <Card>
        <p>{t("admin.dashboardWelcomeBody")}</p>
      </Card>
    </div>
  );
}

export default Dashboard;
