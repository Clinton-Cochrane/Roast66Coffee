// src/components/Customer/Welcome.jsx
import React from 'react';
import Card from '../common/Card';
import logo from "../../logo.png"; // Adjust path if necessary
import { useI18n } from "../../i18n/LanguageContext";


function Welcome() {
  const { t } = useI18n();

  return (
    <div className=" bg-gray-100 flex items-center justify-center">
      <Card title={t("home.welcomeTitle")}>
        <img src={logo} alt={t("home.logoAlt")} className='w-64 mx-auto my-4'/>
        <p className="text-lg text-gray-700">{t("home.tagline")}</p>
      </Card>
    </div>
  );
}

export default Welcome;
