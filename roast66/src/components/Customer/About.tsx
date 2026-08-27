import React from "react";
import { Link } from "react-router-dom";
import {
  FaArrowRight,
  FaInstagram,
  FaMugHot,
  FaTruck,
  FaUsers,
} from "react-icons/fa";
import { useI18n } from "../../i18n/LanguageContext";
import logo from "../../logo-sign.svg";

const instagramUrl = "https://www.instagram.com/roast66coffee/";

function About() {
  const { t } = useI18n();

  return (
    <div className="r66-about">
      <section className="r66-about-hero" aria-labelledby="about-title">
        <svg className="r66-about-road" viewBox="0 0 900 250" aria-hidden="true">
          <path d="M-40 220C170 62 326 298 527 116c122-110 248-107 413-26" />
          <path
            className="r66-about-road-center"
            d="M-40 220C170 62 326 298 527 116c122-110 248-107 413-26"
          />
        </svg>

        <div className="r66-about-shell r66-about-hero-grid">
          <div className="r66-about-hero-copy">
            <p className="r66-about-eyebrow">{t("about.eyebrow")}</p>
            <h1 id="about-title" className="r66-about-title">
              <span>{t("about.titleLead")}</span>
              <span className="r66-about-title-accent">{t("about.titleAccent")}</span>
            </h1>
            <p className="r66-about-intro">{t("about.intro")}</p>

            <div className="r66-about-actions">
              <a
                href={instagramUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="r66-about-primary-action"
              >
                <FaInstagram aria-hidden="true" />
                {t("about.requestTrailer")}
                <FaArrowRight aria-hidden="true" />
                <span className="sr-only"> — {t("about.opensNewWindow")}</span>
              </a>
              <Link to="/menu" className="r66-about-secondary-action">
                {t("about.viewMenu")}
              </Link>
            </div>
            <p className="r66-about-action-note">{t("about.actionNote")}</p>
          </div>

          <aside className="r66-about-story-card" aria-label={t("about.storyLabel")}>
            <div className="r66-about-story-heading">
              <div className="r66-about-logo-mark">
                <img src={logo} alt="" aria-hidden="true" />
              </div>
              <p>{t("about.storyKicker")}</p>
            </div>
            <h2>{t("about.storyTitle")}</h2>
            <p className="r66-about-story-body">{t("about.storyBody")}</p>
            <ul className="r66-about-story-tags" aria-label={t("about.storyHighlightsLabel")}>
              <li>{t("about.storyHighlightOwner")}</li>
              <li>{t("about.storyHighlightMobile")}</li>
              <li>{t("about.storyHighlightLocal")}</li>
            </ul>
          </aside>
        </div>
      </section>

      <section className="r66-about-values" aria-labelledby="about-values-title">
        <div className="r66-about-shell">
          <div className="r66-about-section-heading">
            <p className="r66-about-section-kicker">{t("about.valuesKicker")}</p>
            <h2 id="about-values-title">{t("about.valuesTitle")}</h2>
            <p>{t("about.valuesIntro")}</p>
          </div>

          <div className="r66-about-values-grid">
            <article className="r66-about-value-card">
              <FaMugHot aria-hidden="true" />
              <p className="r66-about-value-number" aria-hidden="true">01</p>
              <h3>{t("about.craftTitle")}</h3>
              <p>{t("about.craftBody")}</p>
            </article>
            <article className="r66-about-value-card">
              <FaTruck aria-hidden="true" />
              <p className="r66-about-value-number" aria-hidden="true">02</p>
              <h3>{t("about.mobileTitle")}</h3>
              <p>{t("about.mobileBody")}</p>
            </article>
            <article className="r66-about-value-card">
              <FaUsers aria-hidden="true" />
              <p className="r66-about-value-number" aria-hidden="true">03</p>
              <h3>{t("about.peopleTitle")}</h3>
              <p>{t("about.peopleBody")}</p>
            </article>
          </div>
        </div>
      </section>

      <section className="r66-about-booking" aria-labelledby="about-booking-title">
        <div className="r66-about-shell r66-about-booking-grid">
          <div className="r66-about-booking-copy">
            <p className="r66-about-booking-kicker">{t("about.bookingKicker")}</p>
            <h2 id="about-booking-title">{t("about.bookingTitle")}</h2>
            <p>{t("about.bookingBody")}</p>
            <a
              href={instagramUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="r66-about-booking-action"
            >
              <FaInstagram aria-hidden="true" />
              {t("about.startRequest")}
              <FaArrowRight aria-hidden="true" />
              <span className="sr-only"> — {t("about.opensNewWindow")}</span>
            </a>
            <p className="r66-about-general-note">{t("about.generalNote")}</p>
          </div>

          <div className="r66-about-request-card">
            <h3>{t("about.requestDetailsTitle")}</h3>
            <p>{t("about.requestDetailsIntro")}</p>
            <ol>
              <li><span aria-hidden="true">1</span>{t("about.requestDate")}</li>
              <li><span aria-hidden="true">2</span>{t("about.requestLocation")}</li>
              <li><span aria-hidden="true">3</span>{t("about.requestGuests")}</li>
              <li><span aria-hidden="true">4</span>{t("about.requestNotes")}</li>
            </ol>
          </div>
        </div>
      </section>
    </div>
  );
}

export default About;
