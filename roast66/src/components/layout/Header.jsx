import PropTypes from "prop-types";
import React from "react";

const Header = ({title, color = "bg-yellow-900"}) => {
    return (
        <header className={`${color} text-white py-4 text-center`}>
            <h1 className="text-3xl font-bold">{title}</h1>
        </header>
    );
};

Header.propTypes = {
    color: PropTypes.string,
    title: PropTypes.string.isRequired
}

export default Header;