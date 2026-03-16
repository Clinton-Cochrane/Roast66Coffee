// src/App.js
import React from 'react';
import { BrowserRouter as Router, Route, Routes, Navigate } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';
import HomePage from './pages/HomePage';
import MenuPage from './pages/MenuPage';
import OrderPage from './pages/OrderPage';
import OrderConfirmationPage from './pages/OrderConfirmationPage';
import OrderStatusPage from './pages/OrderStatusPage';
import Navigation from './components/Navigation';
import './styles/Customer.css';
import './styles/Navigation.css';
import Footer from './components/layout/Footer';
import AdminGate from './components/Admin/AdminGate';

function App() {
  return (
    <Router>
      <div className="App">
        <Navigation />
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/menu" element={<MenuPage />} />
          <Route path="/order" element={<OrderPage />} />
          <Route path="/order/confirmation" element={<OrderConfirmationPage />} />
          <Route path="/order-status" element={<OrderStatusPage />} />
          <Route path="/admin-login" element={<Navigate to="/admin" replace />} />
          <Route path="/admin" element={<AdminGate />} />
        </Routes>
        <Footer/>
        <ToastContainer position="top-right" autoClose={3000} />
      </div>
    </Router>
  );
}

export default App;
