import React, { useEffect, useState } from "react";
import { Link, NavLink, useLocation } from "react-router-dom";
import { FaInstagram, FaBars, FaTimes, FaTshirt, FaShoppingCart, FaMugHot } from "react-icons/fa";

import logo from "../logo.png";
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

function Navigation() {
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const { t } = useI18n();
  const location = useLocation();
  const [orderTrackingSession, setOrderTrackingSession] =
    useState<OrderStatusLookupSessionPayload | null>(() =>
      typeof window !== "undefined" ? readOrderStatusSession() : null
    );

  useEffect(() => {
    setOrderTrackingSession(readOrderStatusSession());
  }, [location.pathname]);

  useEffect(() => {
    const sync = () => setOrderTrackingSession(readOrderStatusSession());
    window.addEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, sync);
    return () => window.removeEventListener(ORDER_STATUS_SESSION_UPDATED_EVENT, sync);
  }, []);

  /** Refresh stored status from the server so the nav dot matches staff updates when not on Order Status. */
  useEffect(() => {
    if (location.pathname === "/order-status") {
      return;
    }

    const session = readOrderStatusSession();
    if (!session) {
      return;
    }
    if (session.orderStatus === ORDER_STATUS.Completed) {
      return;
    }

    let cancelled = false;

    const pull = async () => {
      if (cancelled) {
        return;
      }
      try {
        const data = await fetchOrderLookup(session.trackingToken);
        if (cancelled) {
          return;
        }
        const next = getOrderStatusFromDto(data);
        writeOrderStatusSession(session.trackingToken, next);
      } catch {
        /* ignore */
      }
    };

    const interval = window.setInterval(() => void pull(), 90_000);
    const onVisibility = () => {
      if (document.visibilityState === "visible") {
        void pull();
      }
    };
    document.addEventListener("visibilitychange", onVisibility);
    void pull();

    return () => {
      cancelled = true;
      window.clearInterval(interval);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [
    location.pathname,
    orderTrackingSession?.trackingToken,
    orderTrackingSession?.orderStatus,
  ]);

  const toggleMenu = () => {
    setIsMenuOpen(!isMenuOpen);
  };

  const hasActiveTrackedOrder =
    orderTrackingSession != null &&
    orderTrackingSession.orderStatus !== ORDER_STATUS.Completed;

  return (
    <nav className="w-full border-b border-[#d8c8ba] bg-[#fff9f2]/95 shadow-sm backdrop-blur-sm">
      <div className="relative mx-auto flex w-full max-w-screen-xl items-center justify-between px-4 py-4">
        <div className="text-2xl font-bold">
          <NavLink
            to="/"
            title={t("nav.homeTitle")}
            className="hover:text-[#a64b2a] flex items-center gap-2 transition-colors duration-150"
          >
            <img
              src={logo}
              alt={t("home.logoAlt")}
              width={32}
              height={32}
              className="inline-block h-8"
            />
            <span className="text-[#4a3326]">{t("nav.brandName")}</span>
          </NavLink>
        </div>

        <button
          type="button"
          onClick={toggleMenu}
          className="block md:hidden text-[#4a3326] focus:outline-none"
          aria-expanded={isMenuOpen}
          aria-controls="site-navigation-menu"
          aria-label={isMenuOpen ? t("nav.closeMenu") : t("nav.openMenu")}
        >
          {isMenuOpen ? <FaTimes className="text-2xl" /> : <FaBars className="text-2xl" />}
        </button>

        <ul
          id="site-navigation-menu"
          className={`${
            isMenuOpen ? "block" : "hidden"
          } absolute left-0 top-full z-20 mt-2 w-full items-center rounded border border-[#e2d4c7] bg-[#fff9f2] p-4 shadow-sm md:static md:mt-0 md:flex md:w-auto md:space-x-6 md:rounded-none md:border-0 md:bg-transparent md:p-0 md:shadow-none`}
        >
          <li>
            <NavLink
              to="/menu"
              className={({ isActive }) =>
                `block border-b-2 p-2 no-underline transition-[color,border-color] duration-150 md:inline ${
                  isActive
                    ? "text-[#a64b2a] border-[#a64b2a]"
                    : "text-[#4a3326] border-transparent hover:text-[#a64b2a] hover:border-[#a64b2a]"
                }`
              }
            >
              <span className="inline-flex items-center">
                <FaMugHot className="mr-1 text-xl" aria-hidden="true" />
                {t("nav.menu")}
              </span>
            </NavLink>
          </li>
          <li className="flex flex-wrap items-center gap-1 md:gap-1.5">
            <NavLink
              to="/order"
              className={({ isActive }) =>
                `block border-b-2 p-2 no-underline transition-[color,border-color] duration-150 md:inline ${
                  isActive
                    ? "text-[#a64b2a] border-[#a64b2a]"
                    : "text-[#4a3326] border-transparent hover:text-[#a64b2a] hover:border-[#a64b2a]"
                }`
              }
            >
              <span className="inline-flex items-center">
                <FaShoppingCart className="mr-1 text-xl" aria-hidden="true" />
                {t("nav.order")}
              </span>
            </NavLink>
            {hasActiveTrackedOrder ? (
              <Link
                to={`/order-status?token=${encodeURIComponent(orderTrackingSession.trackingToken)}`}
                className="inline-flex items-center justify-center min-h-[2.5rem] min-w-[1.25rem] shrink-0 p-1 rounded focus:outline-none focus-visible:ring-2 focus-visible:ring-[#a64b2a] focus-visible:ring-offset-2"
                aria-label={t("nav.orderTrackingActive")}
                title={t("nav.orderTrackingActive")}
              >
                <span
                  className="block h-2.5 w-2.5 rounded-full bg-amber-500 ring-2 ring-amber-200"
                  aria-hidden
                />
              </Link>
            ) : null}
          </li>
          <li>
            <a
              href="https://roast-66-coffee.printify.me/products"
              target="_blank"
              rel="noopener noreferrer"
              className="block border-b-2 border-transparent p-2 text-[#4a3326] no-underline transition-[color,border-color] duration-150 hover:border-[#a64b2a] hover:text-[#a64b2a] md:inline"
              title={t("nav.merchTitle")}
            >
              <span className="inline-flex items-center">
                <FaTshirt className="mr-1 text-xl" aria-hidden="true" />
                {t("nav.merch")}
              </span>
            </a>
          </li>
          <li>
            <a
              href="https://www.instagram.com/roast66coffee"
              target="_blank"
              rel="noopener noreferrer"
              className="block border-b-2 border-transparent p-2 text-[#4a3326] no-underline transition-[color,border-color] duration-150 hover:border-[#a64b2a] hover:text-[#a64b2a] md:inline"
              title={t("nav.instagramTitle")}
            >
              <span className="inline-flex items-center">
                <FaInstagram className="mr-1 text-xl" aria-hidden="true" />
                {t("nav.instagram")}
              </span>
            </a>
          </li>
        </ul>
      </div>
    </nav>
  );
}

export default Navigation;
