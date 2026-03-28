import React, { useState, type ChangeEvent, type FormEvent } from "react";
import axios from "../../axiosConfig";
import FormInput from "../common/FormInput";
import Button from "../common/Button";
import Card from "../common/Card";

function Order() {
  const [order, setOrder] = useState({
    customerName: "",
    menuItemId: "",
    quantity: 1,
  });
  const [message, setMessage] = useState("");

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    setOrder({ ...order, [e.target.name]: e.target.value });
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    axios
      .post("/orders", order)
      .then(() => setMessage("Order placed successfully!"))
      .catch(() => setMessage("Failed to place order."));
  };

  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <Card title="Place Order">
        <form onSubmit={handleSubmit} className="space-y-4">
          <FormInput
            type="text"
            name="customerName"
            placeholder="Your Name"
            value={order.customerName}
            onChange={handleChange}
            required
          />
          <FormInput
            type="number"
            name="menuItemId"
            placeholder="Menu Item ID"
            value={order.menuItemId}
            onChange={handleChange}
            required
          />
          <FormInput
            type="number"
            name="quantity"
            placeholder="Quantity"
            value={order.quantity}
            onChange={handleChange}
            min={1}
            required
          />
          <Button type="submit" color="green">
            Place Order
          </Button>
        </form>
        {message ? (
          <p
            className={`mt-4 text-center ${message.includes("successfully") ? "text-green-500" : "text-red-500"}`}
          >
            {message}
          </p>
        ) : null}
      </Card>
    </div>
  );
}

export default Order;
