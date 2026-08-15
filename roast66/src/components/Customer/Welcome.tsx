import React from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/LanguageContext";
import { FaArrowRight, FaMapMarkerAlt, FaMugHot, FaRoad } from "react-icons/fa";

function Welcome() {
  const { t } = useI18n();

  return (
    <section
      className="overflow-hidden rounded-[14px] border border-[#dccdbe] bg-[#fffaf3]/[0.92] shadow-[0_10px_24px_rgba(54,33,19,0.12)] transition-[box-shadow,transform] duration-200 hover:-translate-y-px hover:shadow-[0_14px_30px_rgba(54,33,19,0.14)]"
      aria-labelledby="home-hero-title"
    >
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
              className="inline-flex min-h-12 items-center justify-center gap-2 rounded-md bg-[#4a3326] px-6 py-3 font-bold tracking-wide text-white no-underline shadow-[0_3px_0_rgba(0,0,0,0.18)] transition-[background-color,color,box-shadow,transform] duration-150 motion-safe:hover:-translate-y-px hover:bg-[#2c1d15] hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#99bfdd] focus-visible:ring-offset-2"
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

        <div className="relative flex min-h-64 items-center justify-center overflow-hidden border-t border-[#dccdbe] bg-[#4a3326] px-6 py-8 text-center text-[#fff9f2] md:min-h-full md:border-l md:border-t-0">
          <FaRoad
            className="absolute -bottom-8 -right-8 text-[13rem] text-[#fff9f2]/[0.06]"
            aria-hidden="true"
          />
          <div className="relative">
            <FaMugHot className="mx-auto text-4xl text-[#99bfdd]" aria-hidden="true" />
            <p className="mt-4 text-6xl font-black leading-none tracking-[-0.06em]">66</p>
            <p className="mt-2 text-sm font-bold uppercase tracking-[0.16em] text-[#f4d7bd]">
              {t("home.heroMobileCoffee")}
            </p>
            <p className="mt-5 inline-flex items-center gap-2 text-sm text-[#f7efe6]">
              <FaMapMarkerAlt aria-hidden="true" />
              {t("home.heroFollowRoute")}
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}

export default Welcome;
