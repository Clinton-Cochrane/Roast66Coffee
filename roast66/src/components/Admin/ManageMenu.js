// src/components/Admin/ManageMenu.js
import React, { useEffect, useState } from 'react';
import axios from '../../axiosConfig';
import '../../styles/ManageMenu.css';

function ManageMenu() {
    const [menuItems, setMenuItems] = useState([]);
    const [selectedMenuItemId, setSelectedMenuItemId] = useState('');
    const [menuItemForm, setMenuItemForm] = useState({ name: '', price: '', description: '' });

    useEffect(() => {
        fetchMenuItems();
    }, []);

    const fetchMenuItems = () => {
        axios.get('/admin/menu')
            .then(response => setMenuItems(response.data))
            .catch(error => console.error(error));
    };

    const handleSelectChange = (e) => {
        const selectedId = e.target.value;
        setSelectedMenuItemId(selectedId);

        if (selectedId === 'new') {
            setMenuItemForm({ name: '', price: '', description: '' });
        } else {
            const selectedItem = menuItems.find(item => item.id === parseInt(selectedId));
            if (selectedItem) {
                setMenuItemForm({ name: selectedItem.name, price: selectedItem.price, description: selectedItem.description });
            }
        }
    };

    const handleFormChange = (e) => {
        setMenuItemForm({ ...menuItemForm, [e.target.name]: e.target.value });
    };

    const handleFormSubmit = (e) => {
        e.preventDefault();
        if (selectedMenuItemId === 'new') {
            axios.post('/admin/menu', menuItemForm)
                .then(() => {
                    fetchMenuItems();
                    setMenuItemForm({ name: '', price: '', description: '' });
                    setSelectedMenuItemId('');
                })
                .catch(error => console.error(error));
        } else {
            axios.put(`/admin/menu/${selectedMenuItemId}`, menuItemForm)
                .then(() => {
                    fetchMenuItems();
                    setMenuItemForm({ name: '', price: '', description: '' });
                    setSelectedMenuItemId('');
                })
                .catch(error => console.error(error));
        }
    };

    const handleDelete = (id) => {
        axios.delete(`/admin/menu/${id}`)
            .then(() => fetchMenuItems())
            .catch(error => console.error(error));
    };

    return (
        <div className="manage-menu">
            <h2>Manage Menu</h2>
            <select value={selectedMenuItemId} onChange={handleSelectChange}>
                <option value="">Select a menu item</option>
                <option value="new">Add New Item</option>
                {menuItems.map(item => (
                    <option key={item.id} value={item.id}>{item.name}</option>
                ))}
            </select>
            <form onSubmit={handleFormSubmit}>
                <input type="text" name="name" placeholder="Name" value={menuItemForm.name} onChange={handleFormChange} required />
                <input type="number" name="price" placeholder="Price" value={menuItemForm.price} onChange={handleFormChange} required />
                <textarea name="description" placeholder="Description" value={menuItemForm.description} onChange={handleFormChange} required />
                <button type="submit">{selectedMenuItemId === 'new' ? 'Add Menu Item' : 'Update Menu Item'}</button>
                <Button 
            </form>
            <ul>
                {menuItems.map(item => (
                    <li key={item.id}>
                        {item.name} - ${item.price} - {item.description}
                        <button onClick={() => handleDelete(item.id)}>Delete</button>
                    </li>
                ))}
            </ul>
        </div>
    );
}

export default ManageMenu;
