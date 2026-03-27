import PropTypes from "prop-types";
import React from "react";

const Header = ({title, color = "bg-[#4a3326]"}) => {
    return (
        <header className={`${color} text-[#f7efe6] py-4 text-center border-b border-[#dccdbe]`}>
            <h1 className="text-3xl font-bold tracking-wide">{title}</h1>
        </header>
    );
};

Header.propTypes = {
    color: PropTypes.string,
    title: PropTypes.string.isRequired
}

export default Header;