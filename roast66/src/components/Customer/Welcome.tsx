import React from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/LanguageContext";
import { FaArrowRight } from "react-icons/fa";
import HomePhoto from "./HomePhoto";

function Welcome() {
  const { t } = useI18n();

  return (
    <section className="r66-panel overflow-hidden" aria-labelledby="home-hero-title">
      <div className="grid md:grid-cols-[minmax(0,1.3fr)_minmax(15rem,0.7fr)]">
        <div className="flex flex-col justify-center px-6 py-8 sm:px-9 sm:py-10 md:px-12 md:py-12">
          <p className="mb-3 text-xs font-bold uppercase tracking-[0.16em] text-[#a64b2a]">
            {t("home.heroEyebrow")}
          </p>
          <h1
            id="home-hero-title"
            className="max-w-2xl text-4xl font-bold leading-[1.08] tracking-[-0.025em] text-[#2c1d15] sm:text-5xl"
          >
            {t("home.heroTitle")}
          </h1>
          <p className="mt-4 max-w-xl text-base leading-7 text-[#5b4940] sm:text-lg">
            {t("home.heroBody")}
          </p>

          <div className="mt-7 flex flex-col gap-3 sm:flex-row">
            <Link
              to="/order"
              className="inline-flex min-h-12 items-center justify-center gap-2 rounded-md bg-[#4a3326] px-6 py-3 font-bold tracking-wide text-white no-underline shadow-[0_3px_0_rgba(0,0,0,0.18)] transition-all duration-150 hover:-translate-y-px hover:bg-[#2c1d15] hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#99bfdd] focus-visible:ring-offset-2"
            >
              {t("home.orderNow")}
              <FaArrowRight aria-hidden="true" />
            </Link>
            <Link
              to="/menu"
              className="inline-flex min-h-12 items-center justify-center rounded-md border-2 border-[#4a3326] bg-transparent px-6 py-3 font-bold tracking-wide text-[#4a3326] no-underline transition-colors duration-150 hover:bg-[#f1e4d6] hover:text-[#2c1d15] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#99bfdd] focus-visible:ring-offset-2"
            >
              {t("home.viewMenu")}
            </Link>
          </div>
        </div>

        <HomePhoto
          name="hero"
          alt={t("home.heroImageAlt")}
          pendingLabel={t("home.heroImagePending")}
          width={1440}
          height={960}
          sizes="(min-width: 768px) 35vw, 100vw"
          className="min-h-64 border-t border-[#dccdbe] md:min-h-full md:border-l md:border-t-0"
        />
      </div>
    </section>
  );
}

export default Welcome;
