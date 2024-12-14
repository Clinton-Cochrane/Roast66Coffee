import PropTypes from "prop-types";
import React from "react";

const Header = ({title}) => {
    return (
        <header className="bg-brown text-white py-4 text-center">
            <h1 className="text-3xl font-bold">{title}</h1>
        </header>
    );
};

Header.propTypes = {
    title: PropTypes.string.isRequired
}

export default Header;