import React from "react";
import { useI18n } from "../../i18n/LanguageContext";
import { FaInstagram, FaMapMarkerAlt } from "react-icons/fa";

function Location() {
  const { t } = useI18n();

  return (
    <section className="r66-location-card relative overflow-hidden rounded-2xl bg-[#4a3326] px-6 py-6 text-[#fff9f2] sm:px-8">
      <FaMapMarkerAlt className="absolute -bottom-8 right-5 text-[10rem] text-white/[0.05]" aria-hidden="true" />
      <div className="relative max-w-2xl">
        <h2 className="text-2xl font-bold">{t("home.locationTitle")}</h2>
        <p className="mt-2 leading-7 text-[#f4e7dc]">{t("home.locationBody")}</p>
        <a
          href="https://www.instagram.com/roast66coffee"
          target="_blank"
          rel="noopener noreferrer"
          className="mt-4 inline-flex min-h-11 max-w-full flex-wrap items-center justify-center gap-2 break-words rounded-md bg-[#fff9f2] px-4 py-2 text-center font-bold text-[#4a3326] no-underline hover:bg-white hover:text-[#2c1d15]"
        >
          <FaInstagram aria-hidden="true" />
          {t("home.followInstagram")}
        </a>
      </div>
    </section>
  );
}

export default Location;
