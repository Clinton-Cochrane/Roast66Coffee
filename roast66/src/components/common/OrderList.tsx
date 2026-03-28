import React from "react";
import Button from "./Button";

export type OrderLineItem = {
  name: string;
  price: number;
  quantity: number;
};

type OrderListProps = {
  items: OrderLineItem[];
  onQuantityChange: (index: number, quantity: number) => void;
  onRemove: (index: number) => void;
};

const OrderList = ({ items, onQuantityChange, onRemove }: OrderListProps) => {
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
              min={1}
              onChange={(e) => onQuantityChange(index, parseInt(e.target.value, 10))}
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

export default OrderList;
