// src/components/layout/Navigation.jsx
import React from "react";
import { NavLink } from "react-router-dom";

function Navigation() {
  return (
    <nav className="bg-brown text-white p-4 shadow-md">
      <div className="container mx-auto flex items-center justify-between">
        {/* Brand */}
        <div className="text-2xl font-bold">
          <NavLink to="/" title= "Roast 66 Coffee Home">
            <img src="/roast66/src/logo.svg" alt="Roast 66" className="h-8" />
          </NavLink>
        </div>

        {/* Navigation NavLinks */}
        <ul className="flex space-x-6">
          <li>
            <NavLink to="/" className="hover:text-gray-300">
              Home
            </NavLink>
          </li>
          <li>
            <NavLink
              to="/menu"
              className={({ isActive }) =>
                isActive ? "text-yellow-300" : "hover:text-gray-300"
              }
            >
              Menu
            </NavLink>
          </li>
          <li>
            <NavLink
              to="/order"
              className={({ isActive }) =>
                isActive ? "text-yellow-300" : "hover:text-gray-300"
              }
            >
              Order
            </NavLink>
          </li>
          <li>
            <NavLink
              to="/admin"
              className={({ isActive }) =>
                isActive ? "text-yellow-300" : "hover:text-gray-300"
              }
            >
              Admin
            </NavLink>
          </li>
        </ul>
      </div>
    </nav>
  );
}

export default Navigation;
