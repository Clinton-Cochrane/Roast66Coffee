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

const linkTextClasses = {
  blue: "text-[#6c89a2] hover:text-[#58728a]",
  green: "text-[#4a3326] hover:text-[#2c1d15]",
  red: "text-[#a64b2a] hover:text-[#893b1f]",
  yellow: "text-[#c77e42] hover:text-[#aa6935]",
  gray: "text-[#7b6d62] hover:text-[#66584f]",
};

const solidButtonClasses =
  "text-white py-2 px-4 rounded-md font-semibold tracking-wide shadow-[0_2px_0_rgba(0,0,0,0.16)] transition-all duration-150 hover:-translate-y-[1px] hover:shadow-[0_5px_14px_rgba(74,51,38,0.24)] active:translate-y-0 active:shadow-[0_1px_0_rgba(0,0,0,0.16)] focus:outline-none focus:ring-2 focus:ring-[#99bfdd] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:translate-y-0 disabled:hover:shadow-[0_2px_0_rgba(0,0,0,0.16)]";

const linkButtonClasses =
  "bg-transparent py-0.5 px-0 rounded-sm font-semibold tracking-wide underline underline-offset-[0.2em] shadow-none transition-colors duration-150 focus:outline-none focus:ring-2 focus:ring-[#99bfdd] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60";

const Button = ({
  children,
  onClick,
  type = "button",
  color = "blue",
  variant = "solid",
  disabled = false,
}) => {
  const isLink = variant === "link";
  const colorStyles = isLink ? linkTextClasses[color] : colorClasses[color];
  const shapeStyles = isLink ? linkButtonClasses : solidButtonClasses;

  return (
    <button
      type={type}
      onClick={onClick}
      className={`${colorStyles} ${shapeStyles}`}
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
  variant: PropTypes.oneOf(["solid", "link"]),
  disabled: PropTypes.bool,
};

export default Button;
