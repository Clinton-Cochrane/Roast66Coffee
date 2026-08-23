import React from "react";
import { useI18n } from "../../i18n/LanguageContext";
import HomePhoto from "./HomePhoto";
import Location from "./Location";

function HomeMarketingLocation() {
  const { t } = useI18n();

  return (
    <section className="r66-home-connect" aria-label={t("home.connectSectionLabel")}>
      <HomePhoto
        name="marketing"
        alt={t("home.marketingImageAlt")}
        pendingLabel={t("home.marketingImagePending")}
        width={1200}
        height={630}
        sizes="(min-width: 761px) 30vw, calc(100vw - 1.5rem)"
        className="r66-marketing-photo"
      />
      <Location />
    </section>
  );
}

export default HomeMarketingLocation;
