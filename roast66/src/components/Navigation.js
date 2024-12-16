import React, { useState } from "react";
import { NavLink } from "react-router-dom";
import { FaInstagram, FaBars, FaTimes } from "react-icons/fa";

import logo from "../logo.png"; // Adjust the path if necessary

function Navigation() {
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const toggleMenu = () => {
    setIsMenuOpen(!isMenuOpen);
  };

  return (
    <nav className="bg-primary text-secondary p-4 shadow-md">
      <div className="bg-secondary container mx-auto flex items-center justify-between p-4 rounded">
        {/* Brand Logo */}
        <div className="text-2xl font-bold">
          <NavLink
            to="/"
            title="Roast 66 Coffee Home"
            className="hover:text-accent flex items-center"
          >
            <img src={logo} alt="Roast 66" className="h-8 inline-block mr-2" />
            <span className="text-dark">Roast 66 Coffee</span>
          </NavLink>
        </div>

        {/* Hamburger Menu Button */}
        <button
          onClick={toggleMenu}
          className="block md:hidden text-dark focus:outline-none"
        >
          {isMenuOpen ? (
            <FaTimes className="text-2xl" />
          ) : (
            <FaBars className="text-2xl" />
          )}
        </button>

        {/* Navigation Links */}
        <ul
          className={`${
            isMenuOpen ? "block" : "hidden"
          } md:flex md:space-x-6 items-center w-full md:w-auto md:static absolute top-16 left-0 bg-secondary md:bg-transparent p-4 md:p-0 rounded md:rounded-none`}
        >
          <li>
            <NavLink
              to="/"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
            >
              Home
            </NavLink>
          </li>
          <li>
            <NavLink
              to="/menu"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
            >
              Menu
            </NavLink>
          </li>
          <li>
            <NavLink
              to="/order"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
            >
              Order
            </NavLink>
          </li>
          <li>
            <NavLink
              to="/admin"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
            >
              Admin
            </NavLink>
          </li>
          <li>
            <a
              href="https://www.instagram.com/roast66coffee"
              target="_blank"
              rel="noopener noreferrer"
              className="block md:inline text-dark hover:text-accent no-underline border-b-2 border-transparent hover:border-accent p-2"
              title="Follow us on Instagram"
            >
              <span className="inline-flex items-center">
                <FaInstagram className="text-xl mr-1" />
                Instagram
              </span>
            </a>
          </li>
        </ul>
      </div>
    </nav>
  );
}

export default Navigation;
