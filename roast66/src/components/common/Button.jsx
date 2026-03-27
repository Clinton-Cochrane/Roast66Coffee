import React from "react";
import PropTypes from "prop-types";

// Define a color mapping object
const colorClasses = {
  blue: "bg-[#6c89a2] hover:bg-[#58728a]",
  green: "bg-[#4a3326] hover:bg-[#2c1d15]",
  red: "bg-[#a64b2a] hover:bg-[#893b1f]",
  yellow: "bg-[#c77e42] hover:bg-[#aa6935]",
  gray: "bg-[#7b6d62] hover:bg-[#66584f]",
};

const Button = ({ children, onClick, type = "button", color = "blue", disabled = false }) => {
  return (
    <button
      type={type}
      onClick={onClick}
      className={`${colorClasses[color]} text-white py-2 px-4 rounded-md font-semibold tracking-wide shadow-[0_2px_0_rgba(0,0,0,0.16)] transition-all duration-150 hover:-translate-y-[1px] hover:shadow-[0_5px_14px_rgba(74,51,38,0.24)] active:translate-y-0 active:shadow-[0_1px_0_rgba(0,0,0,0.16)] focus:outline-none focus:ring-2 focus:ring-[#99bfdd] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:translate-y-0 disabled:hover:shadow-[0_2px_0_rgba(0,0,0,0.16)]`}
      disabled={disabled}
    >
      {children}
    </button>
  );
};

Button.propTypes = {
  children: PropTypes.node.isRequired,
  onClick: PropTypes.func,
  type: PropTypes.oneOf(["button", "submit", "reset"]),
  color: PropTypes.oneOf(["blue", "green", "red", "yellow", "gray"]),
  disabled: PropTypes.bool,
};

export default Button;
