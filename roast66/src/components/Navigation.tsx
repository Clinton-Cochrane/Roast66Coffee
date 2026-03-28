import React, { useState } from "react";
import { NavLink } from "react-router-dom";
import { FaInstagram, FaBars, FaTimes, FaTshirt, FaShoppingCart, FaMugHot } from "react-icons/fa";

import logo from "../logo.png";
import { useI18n } from "../i18n/LanguageContext";

function Navigation() {
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const { t } = useI18n();

  const toggleMenu = () => {
    setIsMenuOpen(!isMenuOpen);
  };

  return (
    <nav className="p-4 border-b border-[#d8c8ba] bg-[#f7efe6]/95 backdrop-blur-sm">
      <div className="container mx-auto flex items-center justify-between p-4 rounded-xl border border-[#e2d4c7] bg-[#fff9f2] shadow-sm">
        <div className="text-2xl font-bold">
          <NavLink
            to="/"
            title={t("nav.homeTitle")}
            className="hover:text-[#a64b2a] flex items-center gap-2 transition-colors duration-150"
          >
            <img src={logo} alt={t("home.logoAlt")} className="h-8 inline-block" />
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
          } md:flex md:space-x-6 items-center w-full md:w-auto md:static absolute top-16 mt-2 md:mt-0 left-0 z-20 bg-[#fff9f2] md:bg-transparent p-4 md:p-0 rounded md:rounded-none border border-[#e2d4c7] md:border-0 shadow-sm md:shadow-none`}
        >
          <li>
            <NavLink
              to="/menu"
              className={({ isActive }) =>
                `block md:inline no-underline border-b-2 p-2 transition-all duration-150 ${
                  isActive
                    ? "text-[#a64b2a] border-[#a64b2a]"
                    : "text-[#4a3326] border-transparent hover:text-[#a64b2a] hover:border-[#a64b2a]"
                }`
              }
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
              className={({ isActive }) =>
                `block md:inline no-underline border-b-2 p-2 transition-all duration-150 ${
                  isActive
                    ? "text-[#a64b2a] border-[#a64b2a]"
                    : "text-[#4a3326] border-transparent hover:text-[#a64b2a] hover:border-[#a64b2a]"
                }`
              }
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
              className="block md:inline text-[#4a3326] hover:text-[#a64b2a] no-underline border-b-2 border-transparent hover:border-[#a64b2a] p-2 transition-all duration-150"
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
              className="block md:inline text-[#4a3326] hover:text-[#a64b2a] no-underline border-b-2 border-transparent hover:border-[#a64b2a] p-2 transition-all duration-150"
              title={t("nav.instagramTitle")}
            >
              <span className="inline-flex items-center">
                <FaInstagram className="text-xl mr-1" />
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
