import PropTypes from "prop-types";
import React from "react";

const Footer = ({year = 2024}) => {
    return (
        <footer className="bg=grey-800 text-white py-4 text-center">
            <p>&copy; {year} Roast 66 Coffee. All rights reserved</p>
        </footer>
    );
};

Footer.propTypes = {
    year: PropTypes.number
};

export default Footer;