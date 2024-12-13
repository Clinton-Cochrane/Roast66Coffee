// src/pages/AdminPage.js
import React from 'react';
import ManageMenu from '../components/Admin/ManageMenu';
import ViewOrders from '../components/Admin/ViewOrders';
import '../styles/AdminPage.css';
import NotificationSettings from '../components/Admin/NotificationSettings';

function AdminPage() {
    return (
        <div className="admin-page">
            <h1>Admin Dashboard</h1>
            <ManageMenu />
            <ViewOrders />
            <NotificationSettings />
        </div>
    );
}

export default AdminPage;
