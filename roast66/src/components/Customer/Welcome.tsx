import React from "react";
import { Link } from "react-router-dom";
import { FaArrowRight, FaMugHot } from "react-icons/fa";
import { useI18n } from "../../i18n/LanguageContext";
import logo from "../../logo-sign.svg";
import HomePhoto from "./HomePhoto";

function Welcome() {
  const { t } = useI18n();

  return (
    <section className="r66-hero" aria-labelledby="home-hero-title">
      <svg className="r66-hero-road" viewBox="0 0 700 430" aria-hidden="true">
        <path d="M-30 390C120 310 156 152 330 245c170 90 178-153 397-166" />
        <path
          className="r66-hero-road-center"
          d="M-30 390C120 310 156 152 330 245c170 90 178-153 397-166"
        />
      </svg>

      <div className="r66-hero-inner">
        <div className="r66-hero-copy">
          <p className="r66-hero-eyebrow">{t("home.heroEyebrow")}</p>
          <h1 id="home-hero-title" className="r66-hero-title">
            <span>{t("home.heroTitleLead")}</span>
            <span className="r66-hero-title-accent">{t("home.heroTitleAccent")}</span>
          </h1>
          <div className="r66-hero-rule" aria-hidden="true" />
          <p className="r66-hero-body">{t("home.heroBody")}</p>

          <div className="r66-hero-actions">
            <Link to="/order" className="r66-hero-order">
              <img src={logo} alt="" aria-hidden="true" />
              {t("home.orderNow")}
              <FaArrowRight aria-hidden="true" />
            </Link>
            <Link to="/menu" className="r66-hero-menu">
              {t("home.viewMenu")}
              <FaMugHot aria-hidden="true" />
            </Link>
          </div>
        </div>

        <div className="r66-hero-photo-wrap">
          <HomePhoto
            name="hero"
            alt={t("home.heroImageAlt")}
            pendingLabel={t("home.heroImagePending")}
            width={1440}
            height={960}
            sizes="(min-width: 1024px) 48vw, 100vw"
            tone="light"
            className="r66-hero-photo"
          />
          <img src={logo} alt="" aria-hidden="true" className="r66-hero-watermark" />
        </div>
      </div>
    </section>
  );
}

export default Welcome;
