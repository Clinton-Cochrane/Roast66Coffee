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

Admin Panel: Add, update, or delete menu items.

Seeding Database: Seed initial menu items.

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

cd frontend
npm install

Backend

cd backend

Environment Variables

Create .env files for both the frontend and backend.

Frontend .env

REACT_APP_API_URL=http://localhost:5000/api

Backend appsettings.json

{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=coffeedb;Username=your-username;Password=your-password"
  }
}

Running the App

Using Docker Compose

In the root directory, run:

docker-compose up --build

This will:

Start the frontend on http://localhost:3000

Start the backend on http://localhost:5000

Set up the PostgreSQL database

Running Without Docker

Start the Backend

cd backend

# Apply migrations
 dotnet ef database update

# Run the backend
 dotnet run

Backend should now be running at http://localhost:5000.

Start the Frontend

cd frontend
npm start

Frontend should now be running at http://localhost:3000.

Database Migrations

If you need to add new migrations:

cd backend

# Create a new migration
 dotnet ef migrations add MigrationName

# Apply the migration
 dotnet ef database update

Seeding the Database

Seed the database with initial menu items by hitting the following endpoint:

POST http://localhost:5000/api/admin/seed-menu

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


