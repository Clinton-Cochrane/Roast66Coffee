// src/components/Admin/ViewOrders.js
import React, { useEffect, useState } from 'react';
import axios from '../../axiosConfig';
import '../../styles/ViewOrders.css';

function ViewOrders() {
    const [orders, setOrders] = useState([]);
  
    useEffect(() => {
      fetchOrders();
    }, []);
  
    const fetchOrders = () => {
      axios.get('/admin/orders')
        .then(response => setOrders(response.data))
        .catch(error => console.error(error));
    };
  
    return (
      <div>
        <h1>View Orders</h1>
        {orders.length === 0 ? (
          <p>No orders available</p>
        ) : (
          <ul>
            {orders.map(order => (
              <li key={order.id}>
                <p>Customer: {order.customerName}</p>
                <p>Date: {new Date(order.orderDate).toLocaleString()}</p>
                <ul>
                  {(order.orderItems || []).map(item => (
                    <li key={item.id}>
                      {item.menuItem && item.menuItem.name ? (
                        <>
                          <span>Item: {item.menuItem.name}</span>
                          <span>Quantity: {item.quantity}</span>
                        </>
                      ) : (
                        <span>Item data is unavailable</span>
                      )}
                    </li>
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        )}
      </div>
    );
  }
  
  export default ViewOrders;