// src/components/common/Card.jsx
import PropTypes from "prop-types";
import React from "react";
import classNames from "classnames";


const Card = ({ title, children, className }) => {
  return (
    <div className={classNames("bg-white shadow-md border border-gray-200 rounded-lg p-4 w-full", className)}>
      {title && <h2 className="text-xl font-bold mb-2">{title}</h2>}
      {children}
    </div>
  );
};

Card.propTypes = {
  title: PropTypes.string,
  children: PropTypes.node.isRequired,
  className: PropTypes.string,
};

export default Card;
