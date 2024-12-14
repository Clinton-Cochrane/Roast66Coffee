import React, { useEffect, useState } from "react";
import axios from "../axiosConfig";
import "../styles/OrderPage.css";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";

function OrderPage() {
  const [menuItems, setMenuItems] = useState([]);
  const [orderItems, setOrderItems] = useState([]);
  const [customerName, setCustomerName] = useState("");

  useEffect(() => {
    fetchMenuItems();
  }, []);

  const fetchMenuItems = () => {
    axios
      .get("/admin/menu")
      .then((response) => setMenuItems(response.data))
      .catch((error) => console.error(error));
  };

  const addItemToOrder = (item) => {
    setOrderItems([...orderItems, { ...item, quantity: 1 }]);
  };

  const handleQuantityChange = (index, quantity) => {
    const newOrderItems = [...orderItems];
    newOrderItems[index].quantity = quantity;
    setOrderItems(newOrderItems);
  };

  const handleRemoveItem = (index) => {
    const newOrderItems = orderItems.filter((_, i) => i !== index);
    setOrderItems(newOrderItems);
  };

  const handleOrderSubmit = (e) => {
    e.preventDefault();
    const orderData = {
      customerName,
      orderItems: orderItems.map((item) => ({
        menuItemId: item.id,
        quantity: item.quantity,
      })),
    };
    axios
      .post("/order", orderData)
      .then(() => {
        setOrderItems([]);
        setCustomerName("");
        alert("Order placed successfully!");
      })
      .catch((error) => console.error(error));
  };

  return (
    <div className="p-6">
      <h1 className="text-3xl font-bold mb-6">Place Your Order</h1>

      <select
        onChange={(e) => addItemToOrder(JSON.parse(e.target.value))}
        className="w-full p-2 border rounded mb-4"
      >
        <option value="">Select a menu item</option>
        {menuItems.map((item) => (
          <option key={item.id} value={JSON.stringify(item)}>
            {item.name} - ${item.price}
          </option>
        ))}
      </select>

      <form onSubmit={handleOrderSubmit} className="space-y-4">
       <FormInput
          type="text"
          placeholder="Your Name"
          value={customerName}
          onChange={(e) => setCustomerName(e.target.value)}
          required
        />


        <ul className="space-y-2">
          {orderItems.map((item, index) => (
            <li key={index} className="flex justify-between items-center">
              <span>
                {item.name} - ${item.price}{" "}
              </span>
              <FormInput
                type="number"
                value={item.quantity}
                min="1"
                onChange={(e) => handleQuantityChange(index, e.target.value)}
                required
              />
              <Button
                type="button"
                onClick={() => handleRemoveItem(index)}
                color="red"
              >
                Remove
              </Button>
            </li>
          ))}
        </ul>

        <Button type="submit" color="green">
          Place Order
        </Button>
      </form>
    </div>
  );
}

export default OrderPage;
