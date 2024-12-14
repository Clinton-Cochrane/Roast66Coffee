// src/pages/AdminPage.js
import React from 'react';
import ManageMenu from '../components/Admin/ManageMenu';
import ViewOrders from '../components/Admin/ViewOrders';
import Header from '../components/layout/Header';
import '../styles/AdminPage.css';
import NotificationSettings from '../components/Admin/NotificationSettings';

function AdminPage() {
    return (
        <div className="min-h-screen bg-gray-100 p-6 space-y-6">
            <Header title ="Admin Dashboard"/>
            <ManageMenu />
            <ViewOrders />
            <NotificationSettings />
        </div>
    );
}

export default AdminPage;
