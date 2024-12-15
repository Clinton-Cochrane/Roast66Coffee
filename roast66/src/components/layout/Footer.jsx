import PropTypes from "prop-types";
import React from "react";

const Footer = ({ year = 2024 }) => {
  return (
    <footer className="bg-gray-800 text-white py-4">
      <div className="container mx-auto flex flex-col md:flex-row justify-between items-center px-4">
        <p className="text-sm mb-2 md:mb-0">&copy; {year} Roast 66 Coffee. All rights reserved.</p>

        <div className="flex items-center space-x-4">
          <a
            href="https://www.instagram.com/roast66coffee/"
            target="_blank"
            rel="noopener noreferrer"
            className="text-white hover:text-pink-500 transition duration-300"
          >
            <span className="sr-only">Instagram</span>
            <svg
              className="w-6 h-6"
              fill="currentColor"
              viewBox="0 0 24 24"
              xmlns="http://www.w3.org/2000/svg"
            >
              <path d="M7.75 2C4.022 2 2 4.022 2 7.75v8.5C2 19.978 4.022 22 7.75 22h8.5c3.728 0 5.75-2.022 5.75-5.75v-8.5C22 4.022 19.978 2 16.25 2h-8.5zM12 8.5a3.5 3.5 0 110 7 3.5 3.5 0 010-7zm4.5-.5a1 1 0 11-2 0 1 1 0 012 0zm-4.5 1a4.5 4.5 0 100 9 4.5 4.5 0 000-9z" />
            </svg>
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
