import React, { useEffect, useState } from "react";
import { Link, NavLink, useLocation } from "react-router-dom";
import { FaBars, FaChevronDown, FaTimes } from "react-icons/fa";

import logo from "../logo-sign.svg";
import { ORDER_STATUS } from "../constants/orderStatus";
import { getOrderStatusFromDto } from "../constants/orderStatusParse";
import {
  ORDER_STATUS_SESSION_UPDATED_EVENT,
  readOrderStatusSession,
  writeOrderStatusSession,
  type OrderStatusLookupSessionPayload,
} from "../constants/orderStatusSession";
import { fetchOrderLookup } from "../lib/orderStatusLookup";
import { useI18n } from "../i18n/LanguageContext";

const merchUrl = "https://roast-66-coffee.printify.me/products";

function Navigation() {
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const { locale, setLocale, t } = useI18n();
  const location = useLocation();
  const [orderTrackingSession, setOrderTrackingSession] =
    useState<OrderStatusLookupSessionPayload | null>(() =>
      typeof window !== "undefined" ? readOrderStatusSession() : null
    );

  useEffect(() => {
    setIsMenuOpen(false);
    setOrderTrackingSession(readOrderStatusSession());
  }, [location.pathname, location.hash]);

  useEffect(() => {
    const sync = () => setOrderTrackingSession(readOrderStatusSession());
    window.addEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, sync);
    return () => window.removeEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, sync);
  }, []);

  useEffect(() => {
    if (location.pathname === "/order-status") return;

    const session = readOrderStatusSession();
    if (!session || session.orderStatus === ORDER_STATUS.Completed) return;

    let cancelled = false;
    const pull = async () => {
      if (cancelled) return;
      try {
        const data = await fetchOrderLookup(session.trackingToken);
        if (!cancelled) {
          writeOrderStatusSession(session.trackingToken, getOrderStatusFromDto(data));
        }
      } catch {
        /* Keep navigation usable if background status refresh fails. */
      }
    };

    const interval = window.setInterval(() => void pull(), 90_000);
    const onVisibility = () => {
      if (document.visibilityState === "visible") void pull();
    };
    document.addEventListener("visibilitychange", onVisibility);
    void pull();

    return () => {
      cancelled = true;
      window.clearInterval(interval);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [location.pathname, orderTrackingSession?.trackingToken, orderTrackingSession?.orderStatus]);

  const hasActiveTrackedOrder =
    orderTrackingSession != null && orderTrackingSession.orderStatus !== ORDER_STATUS.Completed;

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `r66-nav-link ${isActive ? "r66-nav-link-active" : ""}`;

  return (
    <header className="r66-header">
      <nav className="r66-header-inner" aria-label={t("nav.primaryNavigation")}>
        <NavLink to="/" title={t("nav.homeTitle")} className="r66-brand">
          <img src={logo} alt={t("home.logoAlt")} className="r66-brand-logo" />
          <span className="min-w-0">
            <span className="r66-brand-name">{t("nav.brandName")}</span>
            <span className="r66-brand-motto">{t("nav.motto")}</span>
          </span>
        </NavLink>

        <button
          type="button"
          onClick={() => setIsMenuOpen((open) => !open)}
          className="r66-mobile-toggle"
          aria-expanded={isMenuOpen}
          aria-controls="site-navigation-menu"
          aria-label={isMenuOpen ? t("nav.closeMenu") : t("nav.openMenu")}
        >
          {isMenuOpen ? <FaTimes aria-hidden="true" /> : <FaBars aria-hidden="true" />}
        </button>

        <div id="site-navigation-menu" className={`r66-navigation-menu ${isMenuOpen ? "is-open" : ""}`}>
          <ul className="r66-primary-links">
            <li>
              <NavLink to="/" end className={navLinkClass}>
                {t("nav.home")}
              </NavLink>
            </li>
            <li>
              <details className="r66-shop-menu">
                <summary className="r66-nav-link">
                  {t("nav.shop")}
                  <FaChevronDown aria-hidden="true" />
                </summary>
                <ul className="r66-shop-dropdown">
                  <li>
                    <NavLink to="/menu" className={navLinkClass}>
                      {t("nav.menu")}
                    </NavLink>
                  </li>
                  <li>
                    <a href={merchUrl} target="_blank" rel="noopener noreferrer" className="r66-nav-link">
                      {t("nav.merch")}
                    </a>
                  </li>
                </ul>
              </details>
            </li>
            <li>
              <Link to="/#home-story" className="r66-nav-link">
                {t("nav.contactAbout")}
              </Link>
            </li>
            <li>
              <a
                href="https://www.instagram.com/roast66coffee"
                target="_blank"
                rel="noopener noreferrer"
                className="r66-nav-link"
                title={t("nav.instagramTitle")}
              >
                {t("nav.instagram")}
              </a>
            </li>
          </ul>

          <div className="r66-header-actions">
            <button
              type="button"
              onClick={() => setLocale(locale === "en" ? "es" : "en")}
              className="r66-language-switch"
              aria-label={locale === "en" ? t("language.switchToSpanish") : t("language.switchToEnglish")}
            >
              {t("language.localeCodeEn")} / {t("language.localeCodeEs")}
            </button>

            <span className="relative">
              <NavLink to="/order" className="r66-order-now">
                {t("nav.orderNow")}
                <img src={logo} alt="" aria-hidden="true" />
              </NavLink>
              {hasActiveTrackedOrder ? (
                <Link
                  to={`/order-status?token=${encodeURIComponent(orderTrackingSession.trackingToken)}`}
                  className="r66-tracking-link"
                  aria-label={t("nav.orderTrackingActive")}
                  title={t("nav.orderTrackingActive")}
                >
                  <span aria-hidden="true" />
                </Link>
              ) : null}
            </span>
          </div>
        </div>
      </nav>
    </header>
  );
}

export default Navigation;
