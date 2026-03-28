import React from "react";
import { FaInstagram } from "react-icons/fa";
import { useI18n } from "../../i18n/LanguageContext";

type FooterProps = {
  year?: number;
};

const Footer = ({ year = 2024 }: FooterProps) => {
  const { locale, setLocale, t } = useI18n();

  return (
    <footer className="bg-[#2c1d15] text-[#f7efe6] py-5 border-t border-[#4a3326]">
      <div className="container mx-auto flex flex-col md:flex-row justify-between items-center px-4 gap-2">
        <p className="text-sm mb-2 md:mb-0">
          &copy; {year} Roast 66 Coffee. {t("footer.rightsReserved")}
        </p>

        <div className="flex items-center space-x-3">
          <button
            type="button"
            onClick={() => setLocale(locale === "en" ? "es" : "en")}
            aria-label={
              locale === "en" ? t("language.switchToSpanish") : t("language.switchToEnglish")
            }
            className="h-8 w-8 rounded-full border border-[#8a7364] text-[10px] font-bold tracking-[0.08em] text-[#f7efe6] hover:border-[#99bfdd] focus:outline-none focus:ring-2 focus:ring-[#99bfdd]"
          >
            {locale === "en" ? "EN" : "ES"}
          </button>
          <a
            href="https://www.instagram.com/roast66coffee"
            target="_blank"
            rel="noopener noreferrer"
            className="md:inline text-[#f7efe6] no-underline border-b-2 border-transparent hover:border-[#99bfdd] p-2"
            title={t("footer.instagramTitle")}
          >
            <span className="inline-flex items-center">
              {t("footer.instagram")}
              <FaInstagram className="text-xl mr-1" />
            </span>
          </a>
        </div>
      </div>
    </footer>
  );
};

export default Footer;
