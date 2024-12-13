import React, { useEffect, useState } from 'react';
import axios from '../axiosConfig';
import '../styles/OrderPage.css'


function OrderPage() {
  const [menuItems, setMenuItems] = useState([]);
  const [orderItems, setOrderItems] = useState([]);
  const [customerName, setCustomerName] = useState('');

  useEffect(() => {
      fetchMenuItems();
  }, []);

  const fetchMenuItems = () => {
      axios.get('/admin/menu')
          .then(response => setMenuItems(response.data))
          .catch(error => console.error(error));
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
          orderItems: orderItems.map(item => ({
              menuItemId: item.id,
              quantity: item.quantity,
          })),
      };
      axios.post('/order', orderData)
          .then(() => {
              setOrderItems([]);
              setCustomerName('');
              alert('Order placed successfully!');
          })
          .catch(error => console.error(error));
  };

  return (
      <div className="order-page">
          <h1>Place Your Order</h1>
          <select onChange={(e) => addItemToOrder(JSON.parse(e.target.value))}>
              <option value="">Select a menu item</option>
              {menuItems.map(item => (
                  <option key={item.id} value={JSON.stringify(item)}>
                      {item.name} - ${item.price}
                  </option>
              ))}
          </select>

          <form onSubmit={handleOrderSubmit}>
              <input
                  type="text"
                  placeholder="Your Name"
                  value={customerName}
                  onChange={(e) => setCustomerName(e.target.value)}
                  required
              />

              <ul>
                  {orderItems.map((item, index) => (
                      <li key={index}>
                          {item.name} - ${item.price}
                          <input
                              type="number"
                              value={item.quantity}
                              min="1"
                              onChange={(e) => handleQuantityChange(index, e.target.value)}
                              required
                          />
                          <button type="button" onClick={() => handleRemoveItem(index)}>Remove</button>
                      </li>
                  ))}
              </ul>

              <button type="submit">Place Order</button>
          </form>
      </div>
  );
}

export default OrderPage;