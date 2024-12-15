// src/components/Admin/ManageMenu.jsx
import React, { useEffect, useState } from 'react';
import axios from '../../axiosConfig';
import '../../styles/ManageMenu.css';
import FormInput from '../common/FormInput';
import Button from '../common/Button';
import Card from '../common/Card';

function ManageMenu() {
  const [menuItems, setMenuItems] = useState([]);
  const [selectedMenuItemId, setSelectedMenuItemId] = useState("");
  const [menuItemForm, setMenuItemForm] = useState({
    name: "",
    price: "",
    description: "",
  });

  useEffect(() => {
    fetchMenuItems();
  }, []);

  const fetchMenuItems = () => {
    axios
      .get("/admin/menu")
      .then((response) => setMenuItems(response.data))
      .catch((error) => console.error(error));
  };

  const handleSelectChange = (e) => {
    const selectedId = e.target.value;
    setSelectedMenuItemId(selectedId);

    if (selectedId === "new") {
      setMenuItemForm({ name: "", price: 0, description: "" });
    } else {
      const selectedItem = menuItems.find(
        (item) => item.id === parseInt(selectedId)
      );
      if (selectedItem) {
        setMenuItemForm({
          name: selectedItem.name,
          price: selectedItem.price.toString(),
          description: selectedItem.description,
        });
      }
    }
  };

  const handleFormChange = (e) => {
    const { name, value } = e.target;
    setMenuItemForm({
      ...menuItemForm,
      [name]: name === "price" ? parseFloat(value) || 0 : value,
    });
  };

  const handleFormSubmit = (e) => {
    e.preventDefault();
  
    const id = selectedMenuItemId === "new" ? 0 : parseInt(selectedMenuItemId, 10);
  
    const formData = {
      id: id,
      name: menuItemForm.name,
      price: parseFloat(menuItemForm.price) || 0,
      description: menuItemForm.description,
    };
    
    if (selectedMenuItemId === "new") {
      axios
        .post("/admin/menu", formData) // Send formData directly
        .then(() => {
          fetchMenuItems();
          setMenuItemForm({ name: "", price: "", description: "" });
          setSelectedMenuItemId("");
        })
        .catch((error) => console.error(error));
    } else {
      axios
        .put(`/admin/menu/${selectedMenuItemId}`, formData) // Send formData directly
        .then(() => {
          fetchMenuItems();
          setMenuItemForm({ name: "", price: "", description: "" });
          setSelectedMenuItemId("");
        })
        .catch((error) => console.error(error));
    }
  };
  

  const handleDelete = (id) => {
    axios
      .delete(`/admin/menu/${id}`)
      .then(() => fetchMenuItems())
      .catch((error) => console.error(error));
  };

  return (
    <div className="manage-menu min-h-screen bg-gray-100 p-6">
      <h2 className="text-2xl font-bold mb-4">Manage Menu</h2>

      <Card>
        <select
          value={selectedMenuItemId}
          onChange={handleSelectChange}
          className="w-full p-2 border rounded mb-4"
        >
          <option value="">Select a menu item</option>
          <option value="new">Add New Item</option>
          {menuItems.map((item) => (
            <option key={item.id} value={item.id}>
              {item.name}
            </option>
          ))}
        </select>

        <form onSubmit={handleFormSubmit} className="space-y-4">
          <FormInput
            type="text"
            name="name"
            placeholder="Name"
            value={menuItemForm.name}
            onChange={handleFormChange}
            required
          />
          <FormInput
            type="number"
            name="price"
            placeholder="Price"
            value={menuItemForm.price}
            onChange={handleFormChange}
            required
          />
          <FormInput
            type="text"
            name="description"
            placeholder="Description"
            value={menuItemForm.description}
            onChange={handleFormChange}
            required
          />
          <Button type="submit" color="green">
            {selectedMenuItemId === "new"
              ? "Add Menu Item"
              : "Update Menu Item"}
          </Button>
        </form>
      </Card>

      <Card title="Menu Items">
        <ul className="space-y-2">
          {menuItems.map((item) => (
            <li
              key={item.id}
              className="flex justify-between items-center border-b pb-2"
            >
              <span>
                {item.name} - ${item.price} - {item.description}
              </span>
              <Button onClick={() => handleDelete(item.id)} color="red">
                Delete
              </Button>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}

export default ManageMenu;
