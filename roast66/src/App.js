// src/App.js
import React from 'react';
import { BrowserRouter as Router, Route, Routes } from 'react-router-dom';
import HomePage from './pages/HomePage';
import MenuPage from './pages/MenuPage';
import OrderPage from './pages/OrderPage';
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
      </div>
    </Router>
  );
}

export default App;
