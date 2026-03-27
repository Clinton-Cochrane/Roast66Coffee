// src/components/common/Card.jsx
import PropTypes from "prop-types";
import React from "react";
import classNames from "classnames";


const Card = ({ title, children, className }) => {
  return (
    <div
      className={classNames(
        "r66-panel p-5 w-full",
        className
      )}
    >
      {title && <h2 className="text-[1.35rem] font-bold mb-2 tracking-[0.01em] text-[#4a3326]">{title}</h2>}
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
