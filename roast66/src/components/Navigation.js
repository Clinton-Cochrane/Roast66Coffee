import React from 'react';
import { Link } from "react-router-dom";
import "../styles/Navigation.css";

function Navigation() {
  return (
    <nav className="navigation">
      <div className="nav-brand">
        <Link to="/">Roast 66</Link>
      </div>

      <ul className="nav-links">
        <li><Link to="/">Home</Link></li>
        <li><Link to="/menu">Menu</Link></li>
        <li><Link to="/order">Order</Link></li>
        <li><Link to="/Admin">Admin</Link></li>
      </ul>
    </nav>
  );
}

export default Navigation;