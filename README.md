Roast 66 Coffee

Welcome to Roast 66 Coffee, a full-stack web application for managing a coffee shop's menu, orders, and administration tasks. Built with React for the frontend, .NET for the backend, and PostgreSQL for the database.

 

Table of Contents

Features

Tech Stack

Installation

Environment Variables

Running the App

Database Migrations

Seeding the Database

Deployment

Screenshots

License

Features

View Menu: Browse a selection of drinks and offerings.

Place Orders: Submit orders for processing.

Admin Panel: Add, update, or delete menu items. Bulk import/export menu as JSON for mass changes.

Seeding Database: Seed default menu from the Admin dashboard (Bulk Menu Operations).

Responsive Design: Optimized for both desktop and mobile devices.

Tech Stack

Frontend

React (with React Router, Axios, and Tailwind CSS)

React Icons (for icons)

Backend

.NET 8 (ASP.NET Core Web API)

Entity Framework Core

PostgreSQL (database)

Deployment

Render (for frontend and backend hosting)

Docker (for containerization)

Installation

Prerequisites

Ensure you have the following installed:

Node.js (v18 or higher)

.NET 8 SDK

Docker Desktop

PostgreSQL (if running locally)

Clone the Repository

git clone https://github.com/your-username/Roast66.git
cd Roast66

Install Dependencies

Frontend

cd roast66
npm install

Backend

cd CoffeeShopApi

Environment Variables

Copy `.env.example` to `.env` for the frontend. Copy `appsettings.Example.json` for backend config.

Frontend (roast66/.env)

REACT_APP_API_URL=http://localhost:5001/api

Backend (CoffeeShopApi/appsettings.json or env vars)

See `CoffeeShopApi/appsettings.Example.json` for structure. Set Admin:Username, Admin:Password, Jwt:Key, and ConnectionStrings for production.

Running the App

Using Docker Compose

In the root directory, run:

docker-compose up --build

This will:

- Start the frontend on http://localhost:3000
- Start the backend on http://localhost:5001
- Set up the PostgreSQL database

Running Without Docker

Start the Backend

cd CoffeeShopApi
dotnet ef database update
dotnet run

Backend runs at http://localhost:8080 (or port in launchSettings).

Start the Frontend

cd roast66
npm start

Frontend runs at http://localhost:3000.

Database Migrations

cd CoffeeShopApi
dotnet ef migrations add MigrationName
dotnet ef database update

Seeding the Database

Log in to the Admin dashboard at /admin, then use "Seed Default Menu" in the Bulk Menu Operations section. Or call (requires admin auth):

GET /api/Admin/seed-menu?confirm=true

Deployment

Deploying to Render

Push your changes to GitHub:

git add .
git commit -m "Prepare for deployment"
git push origin main

In the Render Dashboard:

Deploy the frontend and backend services.

Seed the database on Render by calling the /api/admin/seed-menu endpoint.


License

This project is licensed under the MIT License.


