// src/App.js
import React from 'react';
import { BrowserRouter as Router, Route, Routes } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';
import HomePage from './pages/HomePage';
import MenuPage from './pages/MenuPage';
import OrderPage from './pages/OrderPage';
import OrderConfirmationPage from './pages/OrderConfirmationPage';
import OrderStatusPage from './pages/OrderStatusPage';
import AdminPage from './pages/AdminPage';
import Navigation from './components/Navigation';
import './styles/Customer.css';
import './styles/Navigation.css';
import Footer from './components/layout/Footer';
import AdminLogin from './pages/AdminLogin';
import PrivateRoute from './components/Admin/PrivateRoute';

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
          <Route path="/admin-login" element={<AdminLogin />} />
          <Route
            path="/admin"
            element={
              <PrivateRoute>
                <AdminPage />
              </PrivateRoute>
            }
          />
        </Routes>
        <Footer/>
        <ToastContainer position="top-right" autoClose={3000} />
      </div>
    </Router>
  );
}

export default App;
