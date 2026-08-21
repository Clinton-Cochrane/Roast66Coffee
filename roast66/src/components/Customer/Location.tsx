import React from "react";
import { useI18n } from "../../i18n/LanguageContext";
import { FaInstagram, FaMapMarkerAlt } from "react-icons/fa";

function Location() {
  const { t } = useI18n();

  return (
    <section className="relative overflow-hidden rounded-2xl bg-[#4a3326] px-7 py-9 text-[#fff9f2] sm:px-10">
      <FaMapMarkerAlt className="absolute -bottom-8 right-5 text-[10rem] text-white/[0.05]" aria-hidden="true" />
      <div className="relative max-w-2xl">
        <h2 className="text-3xl font-bold">{t("home.locationTitle")}</h2>
        <p className="mt-3 text-lg leading-8 text-[#f4e7dc]">{t("home.locationBody")}</p>
        <a
          href="https://www.instagram.com/roast66coffee"
          target="_blank"
          rel="noopener noreferrer"
          className="mt-6 inline-flex min-h-12 items-center gap-2 rounded-md bg-[#fff9f2] px-5 py-3 font-bold text-[#4a3326] no-underline hover:bg-white hover:text-[#2c1d15]"
        >
          <FaInstagram aria-hidden="true" />
          {t("home.followInstagram")}
        </a>
      </div>
    </section>
  );
}

export default Location;
