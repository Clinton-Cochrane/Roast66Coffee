import PropTypes from "prop-types";
import React from "react";
import { FaInstagram} from "react-icons/fa";
import { useI18n } from "../../i18n/LanguageContext";

const Footer = ({ year = 2024 }) => {
  const { t } = useI18n();

  return (
    <footer className="bg-gray-800 text-white py-4">
      <div className="container mx-auto flex flex-col md:flex-row justify-between items-center px-4">
        <p className="text-sm mb-2 md:mb-0">
          &copy; {year} Roast 66 Coffee. {t("footer.rightsReserved")}
        </p>

        <div className="flex items-center space-x-4">
        <a
              href="https://www.instagram.com/roast66coffee"
              target="_blank"
              rel="noopener noreferrer"
              className="md:inline text-white no-underline border-b-2 border-transparent p-2"
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

Footer.propTypes = {
  year: PropTypes.number,
};

export default Footer;
