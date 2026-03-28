import React from "react";
import { useI18n } from "../../i18n/LanguageContext";

function OrderConfirmation() {
  const { t } = useI18n();

  return (
    <div className="order-confirmation">
      <h2>{t("orderConfirmation.standaloneLegacyTitle")}</h2>
      <p>{t("orderConfirmation.standaloneLegacyThankYou")}</p>
    </div>
  );
}

export default OrderConfirmation;
