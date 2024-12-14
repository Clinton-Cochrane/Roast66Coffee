// src/components/layout/Navigation.jsx
import React from "react";
import { NavLink } from "react-router-dom";
import logo from "../logo.png"; // Adjust the path if necessary

function Navigation() {
  return (
    <nav className="bg-primary text-secondary p-4 shadow-md">
      <div className="bg-secondary container mx-auto flex items-center justify-between p-4 rounded">
        {/* Brand Logo */}
        <div className="text-2xl font-bold">
          <NavLink to="/" title="Roast 66 Coffee Home" className="hover:text-accent flex items-center">
            <img src={logo} alt="Roast 66" className="h-8 inline-block mr-2" />
            <span className="text-dark">Roast 66 Coffee</span>
          </NavLink>
        </div>

        {/* Navigation Links */}
        <ul className="flex space-x-6">
          <li>
            <NavLink to="/" className="text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent">
              Home
            </NavLink>
          </li>
          <li>
            <NavLink to="/menu" className="text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent">
              Menu
            </NavLink>
          </li>
          <li>
            <NavLink to="/order" className="text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent">
              Order
            </NavLink>
          </li>
          <li>
            <NavLink to="/admin" className="text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent">
              Admin
            </NavLink>
          </li>
        </ul>
      </div>
    </nav>
  );
}

export default Navigation;
