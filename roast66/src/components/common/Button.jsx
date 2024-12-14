import React from "react";
import PropTypes from "prop-types";

const Button = ({children, onClick, type = 'button', color = 'blue'}) => {
    return (
        <button type = {type} onClick={onClick} className={`bg-${color}-500 hover:bg-${color}-700 text-white py-2 px-4 rounded`}>
            {children}
        </button>
    )
}

Button.propTypes = {
    children: PropTypes.node.isRequired,
    onClick: PropTypes.func,
    type: PropTypes.oneOf(["button", "submit", "reset"]),
    color: PropTypes.string,
  };
  

export default Button;