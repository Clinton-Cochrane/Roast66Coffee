// src/components/Customer/Menu.jsx
import React, { useEffect, useState } from 'react';
import axios from '../../axiosConfig';
import Card from '../common/Card';

function Menu() {
  const [menuItems, setMenuItems] = useState([]);

  useEffect(() => {
    axios.get('/menu')
      .then(response => setMenuItems(response.data))
      .catch(error => console.error(error));
  }, []);

  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <h2 className="text-3xl font-bold mb-6 text-center">Our Menu</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {menuItems.map(item => (
          <Card key={item.id}>
            <h3 className="text-xl font-semibold mb-2">{item.name}</h3>
            <p className="text-gray-700">${item.price.toFixed(2)}</p>
          </Card>
        ))}
      </div>
    </div>
  );
}

export default Menu;
