import React from "react";
import PropTypes from "prop-types";

const Card = ({title, children}) => {
    return (
        <div className="bg-white shadow-md rounded-lg p-4 mb-4">
            {title && <h2 className="text-xl font-bold mb-2">{title}</h2>}
            {children}
        </div>
    )
}

Card.propTypes = {
    title: PropTypes.string,
    children: PropTypes.node.isRequired,
};


export default Card;