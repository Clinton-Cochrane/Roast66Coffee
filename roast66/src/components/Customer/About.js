// src/components/Customer/About.jsx
import React from 'react';
import Card from '../common/Card';
import { useI18n } from "../../i18n/LanguageContext";

function About() {
  const { t } = useI18n();

  return (
    <div >
      <Card className="min-h-40" title={t("home.aboutTitle")}>
        <p className="mb-4">
          {t("home.aboutBodyOne")}
        </p>
        <p>
          {t("home.aboutBodyTwo")}
        </p>
      </Card>
    </div>
  );
}

export default About;
