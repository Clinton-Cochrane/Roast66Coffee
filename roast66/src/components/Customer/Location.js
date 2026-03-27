// src/components/Customer/Location.js
import React from 'react';
import Card from "../common/Card"
import { useI18n } from "../../i18n/LanguageContext";

function Location() {
  const { t } = useI18n();

  return (
    <div>
      <Card className="min-h-40" title={t("home.locationTitle")}>
      <p>{t("home.locationBody")}</p>
      </Card>
      
    </div>
  );
}

export default Location;
