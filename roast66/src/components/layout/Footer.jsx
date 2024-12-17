import PropTypes from "prop-types";
import React from "react";
import { FaInstagram} from "react-icons/fa";

const Footer = ({ year = 2024 }) => {
  return (
    <footer className="bg-gray-800 text-white py-4">
      <div className="container mx-auto flex flex-col md:flex-row justify-between items-center px-4">
        <p className="text-sm mb-2 md:mb-0">&copy; {year} Roast 66 Coffee. All rights reserved.</p>

        <div className="flex items-center space-x-4">
        <a
              href="https://www.instagram.com/roast66coffee"
              target="_blank"
              rel="noopener noreferrer"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
              title="Follow us on Instagram"
            >
              <span className="inline-flex items-center">
                <FaInstagram className="text-xl mr-1" />
                Instagram
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
