import React, { useState } from "react";
import { NavLink } from "react-router-dom";
import {
  FaInstagram,
  FaBars,
  FaTimes,
  FaTshirt,
  FaShoppingCart,
  FaMugHot,
} from "react-icons/fa";

import logo from "../logo.png"; // Adjust the path if necessary
import { useI18n } from "../i18n/LanguageContext";

function Navigation() {
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const { locale, setLocale, t } = useI18n();

  const toggleMenu = () => {
    setIsMenuOpen(!isMenuOpen);
  };

  return (
    <nav className="bg-primary text-secondary p-4 shadow-md">
      <div className="bg-secondary container mx-auto flex items-center justify-between p-4 rounded">
        {/* Brand Logo */}
        <div className="text-2xl font-bold">
          <NavLink
            to="/"
            title={t("nav.homeTitle")}
            className="hover:text-accent flex items-center"
          >
            <img src={logo} alt={t("home.logoAlt")} className="h-8 inline-block mr-2" />
            <span className="text-dark">{t("nav.brandName")}</span>
          </NavLink>
        </div>

        {/* Hamburger Menu Button */}
        <button
          onClick={toggleMenu}
          className="block md:hidden text-dark focus:outline-none"
          aria-expanded={isMenuOpen}
          aria-controls="site-navigation-menu"
          aria-label={isMenuOpen ? t("nav.closeMenu") : t("nav.openMenu")}
        >
          {isMenuOpen ? (
            <FaTimes className="text-2xl" />
          ) : (
            <FaBars className="text-2xl" />
          )}
        </button>

        {/* Navigation Links */}
        <ul
          id="site-navigation-menu"
          className={`${
            isMenuOpen ? "block" : "hidden"
          } md:flex md:space-x-6 items-center w-full md:w-auto md:static absolute top-16 left-0 bg-secondary md:bg-transparent p-4 md:p-0 rounded md:rounded-none`}
        >
          <li>
            <NavLink
              to="/menu"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
            >
              <span className="inline-flex items-center">
            <FaMugHot className="text-xl mr-1" />
            {t("nav.menu")}
          </span>
            </NavLink>
          </li>
          <li>
            <NavLink
              to="/order"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
            >
              <span className="inline-flex items-center">
                <FaShoppingCart className="text-xl mr-1" />
                {t("nav.order")}
              </span>
            </NavLink>
          </li>
          <li>
            <a
              href="https://roast-66-coffee.printify.me/products"
              target="_blank"
              rel="noopener noreferrer"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
              title={t("nav.merchTitle")}
            >
              <span className="inline-flex items-center">
                <FaTshirt className="text-xl mr-1" />
                {t("nav.merch")}
              </span>
            </a>
          </li>
          <li>
            <a
              href="https://www.instagram.com/roast66coffee"
              target="_blank"
              rel="noopener noreferrer"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
              title={t("nav.instagramTitle")}
            >
              <span className="inline-flex items-center">
                <FaInstagram className="text-xl mr-1" />
                {t("nav.instagram")}
              </span>
            </a>
          </li>
          <li className="flex items-center gap-2 mt-2 md:mt-0">
            <button
              type="button"
              onClick={() => setLocale("en")}
              disabled={locale === "en"}
              aria-pressed={locale === "en"}
              aria-label={t("language.switchToEnglish")}
              className="text-dark border border-dark px-2 py-1 rounded disabled:opacity-60"
            >
              {t("language.english")}
            </button>
            <button
              type="button"
              onClick={() => setLocale("es")}
              disabled={locale === "es"}
              aria-pressed={locale === "es"}
              aria-label={t("language.switchToSpanish")}
              className="text-dark border border-dark px-2 py-1 rounded disabled:opacity-60"
            >
              {t("language.spanish")}
            </button>
          </li>
        </ul>
      </div>
    </nav>
  );
}

export default Navigation;
