import React from "react";
import Card from "../common/Card";
import logo from "../../logo.png";
import { useI18n } from "../../i18n/LanguageContext";
import { FaRoad, FaMugHot } from "react-icons/fa";

function Welcome() {
  const { t } = useI18n();

  return (
    <div className="flex items-center justify-center">
      <Card title={t("home.welcomeTitle")} className="text-center">
        <img src={logo} alt={t("home.logoAlt")} className="w-64 mx-auto my-4" />
        <p className="text-lg text-[#4a3326] font-medium">{t("home.tagline")}</p>
        <p className="text-sm text-[#6f5b4b] mt-2">Homemade drink comfort with a Route 66 heart.</p>
        <div className="mt-3 flex items-center justify-center gap-2 text-xs font-semibold uppercase tracking-[0.08em] text-[#6c89a2]">
          <span className="inline-flex items-center gap-1">
            <FaMugHot />
            Small Batch
          </span>
          <span className="text-[#b59e8c]">|</span>
          <span className="inline-flex items-center gap-1">
            <FaRoad />
            Road Ready
          </span>
        </div>
      </Card>
    </div>
  );
}

export default Welcome;
