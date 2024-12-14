import React from "react";
import PropTypes from "prop-types";

// Define a color mapping object
const colorClasses = {
  blue: "bg-blue-500 hover:bg-blue-700",
  green: "bg-green-900 hover:bg-green-500",
  red: "bg-red-500 hover:bg-red-700",
  yellow: "bg-yellow-500 hover:bg-yellow-700",
  gray: "bg-gray-500 hover:bg-gray-700",
};

const Button = ({ children, onClick, type = "button", color = "blue" }) => {
  return (
    <button
      type={type}
      onClick={onClick}
      className={`${colorClasses[color]} text-white py-2 px-4 rounded`}
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
};

export default Button;
