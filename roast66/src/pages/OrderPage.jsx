import React, { useEffect, useState } from "react";
import axios from "../axiosConfig";
import "../styles/OrderPage.css";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";
import CategoryType from "../constants/categories";

function OrderPage() {
  const [menuItems, setMenuItems] = useState([]);
  const [orderItems, setOrderItems] = useState([]);
  const [customerName, setCustomerName] = useState("");
  const [customerPhone, setCustomerPhone] = useState("");

  useEffect(() => {
    fetchMenuItems();
  }, []);

  const fetchMenuItems = () => {
    axios
      .get("/admin/menu")
      .then((response) => setMenuItems(response.data))
      .catch((error) => console.error(error));
  };

  const canOrderDirectly = (item) => {
    return (
      item.categoryType === CategoryType.COFFEE ||
      item.categoryType === CategoryType.DRINKS ||
      item.categoryType === CategoryType.SPECIALS
    );
  };

  const addItemToOrder = (item) => {
    if (canOrderDirectly(item)) {
      setOrderItems([
        ...orderItems,
        { ...item, quantity: 1, notes: "", addOns: [] },
      ]);
    } else {
      alert(
        "Flavors cannot be ordered alone. Please add them as add-ons to an existing drink"
      );
    }
  };

  const calculateOrderTotal = () => {
    return orderItems.reduce(
      (total, item) => total + calculateTotalPrice(item),
      0
    );
  };

  const calculateTotalPrice = (item) => {
    const basePrice = item.price * item.quantity;
    const addOnsPrice = item.addOns.reduce(
      (total, addOn) => total + addOn.price * addOn.quantity,
      0
    );

    return basePrice + addOnsPrice;
  };

  const handleQuantityChange = (index, quantity) => {
    const newOrderItems = [...orderItems];
    newOrderItems[index].quantity = quantity;
    setOrderItems(newOrderItems);
  };

  const handleNotesChange = (index, notes) => {
    const newOrderItems = [...orderItems];
    newOrderItems[index].notes = notes;
    setOrderItems(newOrderItems);
  };

  const handleRemoveItem = (index) => {
    const newOrderItems = orderItems.filter((_, i) => i !== index);
    setOrderItems(newOrderItems);
  };

  const handleAddFlavor = (index, flavor) => {
    if (!flavor || !flavor.id) return;

    const newOrderItems = [...orderItems];
    const addOns = newOrderItems[index].addOns;

    if (!addOns.some((addOn) => addOn.id === flavor.id)) {
      addOns.push({ ...flavor, quantity: 1 });
      setOrderItems(newOrderItems);
    } else {
      alert("This flavor has already been added.");
    }

    // Reset the dropdown
    document.getElementById(`flavor-select-${index}`).value = "";
  };

  const handleOrderSubmit = (e) => {
    e.preventDefault();
    const orderData = {
      customerName,
      customerPhone,
      orderItems: orderItems.map((item) => ({
        menuItemId: item.id,
        quantity: item.quantity,
        notes: item.notes,
        addOns: item.addOns.map((addOn) => ({
          menuItemId: addOn.id,
          quantity: addOn.quantity,
        })),
      })),
    };
    axios
      .post("/order", orderData)
      .then(() => {
        setOrderItems([]);
        setCustomerName("");
        setCustomerPhone("");
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
        {menuItems
          .filter(
            (item) =>
              item.categoryType === CategoryType.COFFEE ||
              item.categoryType === CategoryType.DRINKS ||
              item.categoryType === CategoryType.SPECIALS
          )
          .map((item) => (
            <option key={item.id} value={JSON.stringify(item)}>
              {item.name} - ${item.price} - {item.description}
            </option>
          ))}
      </select>

      <form onSubmit={handleOrderSubmit} className="space-y-4">
        <ul className="space-y-4">
          {orderItems.map((item, index) => (
            <li
              key={index}
              className="flex flex-col space-y-2 p-4 border rounded shadow"
            >
              <div className="flex justify-between items-center">
                <span className="font-bold">
                  {item.name} - ${calculateTotalPrice(item).toFixed(2)}
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
              </div>
              <FormInput
                type="text"
                placeholder="Notes (optional)"
                value={item.notes}
                onChange={(e) => handleNotesChange(index, e.target.value)}
                className="placeholder-gray-400"
              />
              <div>
                <select
                  id={`flavor-select-${index}`}
                  onChange={(e) =>
                    handleAddFlavor(index, JSON.parse(e.target.value))
                  }
                  className="w-full p-2 border rounded mb-2"
                >
                  <option value="">Add a Flavor</option>
                  {menuItems
                    .filter(
                      (menuItem) =>
                        menuItem.categoryType === CategoryType.FLAVORS
                    )
                    .map((flavor) => (
                      <option key={flavor.id} value={JSON.stringify(flavor)}>
                        {flavor.name} - ${flavor.price}
                      </option>
                    ))}
                </select>

                {item.addOns.length > 0 && (
                  <ul className="list-disc ml-5">
                    {item.addOns.map((addOn, addOnIndex) => (
                      <li
                        key={addOnIndex}
                        className="flex items-center space-x-2"
                      >
                        <span>
                          {addOn.name} - ${addOn.price} x {addOn.quantity}
                        </span>
                        <FormInput
                          type="number"
                          min="1"
                          value={addOn.quantity}
                          onChange={(e) => {
                            const newOrderItems = [...orderItems];
                            newOrderItems[index].addOns[addOnIndex].quantity =
                              parseInt(e.target.value, 10);
                            setOrderItems(newOrderItems);
                          }}
                        />
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </li>
          ))}
        </ul>

        <FormInput
          type="text"
          placeholder="Your Name"
          value={customerName}
          onChange={(e) => setCustomerName(e.target.value)}
          required
        />
        <FormInput
          type="text"
          placeholder="Phone For When Order Is Ready"
          value={customerPhone}
          onChange={(e) => setCustomerPhone(e.target.value)}
          required
        />
        <div className="font-bold text-lg">
          Total: ${calculateOrderTotal().toFixed(2)}
        </div>

        <Button type="submit" color="green">
          Place Order
        </Button>
      </form>
    </div>
  );
}

export default OrderPage;
