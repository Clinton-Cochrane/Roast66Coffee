import React, { useState } from "react";
import axios from "../../axiosConfig";

function Order() {
  const [order, setOrder] = useState({
    customerName: "",
    menuItemId: "",
    quantity: 1,
  });
  const [message, setMessage] = useState("");

  const handleChange = (e) => {
    setOrder({ ...order, [e.target.name]: e.target.value });
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    axios
      .post("/orders", order)
      .then((response) => setMessage("Order placed successfully!"))
      .catch((error) => setMessage("Failed to place order."));
  };

  return (
    <div className="order">
      <h2>Place Order</h2>
      <form onSubmit={handleSubmit}>
        <input
          type="text"
          name="customerName"
          placeholder="Your Name"
          onChange={handleChange}
          required
        />
        <input
          type="number"
          name="menuItemId"
          placeholder="Menu Item ID"
          onChange={handleChange}
          required
        />
        <input
          type="number"
          name="quantity"
          placeholder="Quantity"
          onChange={handleChange}
          min="1"
          required
        />
        <button type="submit">Order</button>
      </form>
      {message && <p>{message}</p>}
    </div>
  );
}

export default Order;
