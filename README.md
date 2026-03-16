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

Copy `.env.example` to `.env` for the frontend. For the backend, copy `CoffeeShopApi/appsettings.Example.json` to `CoffeeShopApi/appsettings.json` (appsettings.json is gitignored and must be created locally).

Frontend (roast66/.env)

REACT_APP_API_URL=http://localhost:5001/api

Backend (CoffeeShopApi/appsettings.json or environment variables)

See `CoffeeShopApi/appsettings.Example.json` for the full structure. Required keys:

- `ConnectionStrings:DefaultConnection` - PostgreSQL connection string
- `Admin:Username`, `Admin:Password` - Admin login credentials
- `Jwt:Key` (min 32 chars), `Jwt:Issuer`, `Jwt:Audience` - JWT configuration
- `AllowedOrigins` - Comma-separated list of allowed frontend URLs (required in production)

Running the App

Using Docker Compose

Copy `env.example` to `.env` in the repo root to customize Postgres credentials (optional; defaults to root/toor for local dev only).

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

Backend runs at http://localhost:80 (set `PORT` env var for a different port).

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

Production Deployment (Render)

This project includes a `render.yaml` Blueprint for one-click deployment to Render.

1. Push your code to GitHub and connect the repository to Render.
2. In Render Dashboard, create a new Blueprint Instance and select this repo. Render will create:
   - A PostgreSQL database (`roast66-db`)
   - Backend API (`roast66-api`) - Docker-based
   - Frontend static site (`roast66-web`)
3. Set the following environment variables in the Render Dashboard (they are marked `sync: false` and must be entered manually):

   **Backend (roast66-api):**
   - `Admin__Username` - Admin login username
   - `Admin__Password` - Admin login password (use a strong password)
   - `Jwt__Key` - Secret key for JWT signing (min 32 characters)
   - `AllowedOrigins` - Comma-separated frontend URLs, e.g. `https://roast66-web.onrender.com,https://yourdomain.com`

   **Frontend (roast66-web):**
   - `REACT_APP_API_URL` - Backend API URL, e.g. `https://roast66-api.onrender.com/api`

4. After the first deploy, you may need to redeploy the backend so `AllowedOrigins` includes the actual frontend URL, and redeploy the frontend so `REACT_APP_API_URL` points to the actual backend URL.
5. Seed the database: Log in to Admin at `/admin`, then use "Seed Default Menu" in Bulk Menu Operations. Or call `GET /api/Admin/seed-menu?confirm=true` with admin auth.


License

This project is licensed under the MIT License.


