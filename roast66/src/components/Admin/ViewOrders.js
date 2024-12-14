// src/components/Admin/ViewOrders.jsx
import React, { useEffect, useState } from 'react';
import axios from '../../axiosConfig';
import Card from '../common/Card';
import Button from '../common/Button';

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
    <div className="min-h-screen bg-gray-100 p-6">
      <h1 className="text-3xl font-bold mb-6">View Orders</h1>

      {orders.length === 0 ? (
        <p className="text-gray-500">No orders available</p>
      ) : (
        <div className="space-y-4">
          {orders.map(order => (
            <Card key={order.id} title={`Order #${order.id}`}>
              <p className="mb-2"><strong>Customer:</strong> {order.customerName}</p>
              <p className="mb-4"><strong>Date:</strong> {new Date(order.orderDate).toLocaleString()}</p>

              <ul className="space-y-2">
                {(order.orderItems || []).map(item => (
                  <li key={item.id} className="flex justify-between items-center border-b pb-2">
                    {item.menuItem && item.menuItem.name ? (
                      <>
                        <span><strong>Item:</strong> {item.menuItem.name}</span>
                        <span><strong>Quantity:</strong> {item.quantity}</span>
                      </>
                    ) : (
                      <span className="text-red-500">Item data is unavailable</span>
                    )}
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

export default ViewOrders;
