import React from "react";
import { FaInstagram } from "react-icons/fa";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/LanguageContext";
import logo from "../../logo-sign.svg";

type FooterProps = {
  year?: number;
};

const Footer = ({ year = new Date().getFullYear() }: FooterProps) => {
  const { t } = useI18n();

  return (
    <footer className="r66-footer">
      <div className="r66-footer-inner">
        <div className="r66-footer-brand-block">
          <Link to="/" className="r66-footer-brand" title={t("nav.homeTitle")}>
            <img src={logo} alt="" aria-hidden="true" />
            <span>{t("nav.brandName")}</span>
          </Link>
          <span className="r66-footer-divider" aria-hidden="true">•</span>
          <p className="r66-footer-motto">{t("footer.motto")}</p>
        </div>

        <nav className="r66-footer-links" aria-label={t("footer.navigationLabel")}>
          <Link to="/about">{t("nav.contactAbout")}</Link>
          <Link to="/menu">{t("nav.menu")}</Link>
          <Link to="/order">{t("nav.orderNow")}</Link>
          <a href="https://roast-66-coffee.printify.me/products" target="_blank" rel="noopener noreferrer">
            {t("nav.merch")}
          </a>
        </nav>

        <div className="r66-footer-social">
          <a
            href="https://www.instagram.com/roast66coffee"
            target="_blank"
            rel="noopener noreferrer"
            className="r66-footer-social-link"
            title={t("footer.instagramTitle")}
          >
            <FaInstagram aria-hidden="true" />
            <span className="sr-only">{t("footer.instagram")}</span>
          </a>
        </div>
      </div>
      <p className="r66-footer-legal">&copy; {year} Roast 66 Coffee. {t("footer.rightsReserved")}</p>
    </footer>
  );
};

export default Footer;
