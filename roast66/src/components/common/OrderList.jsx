// src/components/common/OrderList.jsx
import React from "react";
import PropTypes from "prop-types";
import Button from "./Button";

const OrderList = ({ items, onQuantityChange, onRemove }) => {
  return (
    <ul className="space-y-2">
      {items.map((item, index) => (
        <li key={index} className="flex justify-between items-center border-b pb-2">
          <div>
            {item.name} - ${item.price}
          </div>
          <div className="flex items-center space-x-2">
            <input
              type="number"
              value={item.quantity}
              min="1"
              onChange={(e) => onQuantityChange(index, parseInt(e.target.value))}
              className="w-16 p-1 border rounded"
            />
            <Button onClick={() => onRemove(index)} color="red">
              Remove
            </Button>
          </div>
        </li>
      ))}
    </ul>
  );
};

OrderList.propTypes = {
  items: PropTypes.arrayOf(
    PropTypes.shape({
      name: PropTypes.string.isRequired,
      price: PropTypes.number.isRequired,
      quantity: PropTypes.number.isRequired,
    })
  ).isRequired,
  onQuantityChange: PropTypes.func.isRequired,
  onRemove: PropTypes.func.isRequired,
};

export default OrderList;
