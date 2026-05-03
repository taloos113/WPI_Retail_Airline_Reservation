# WPI Retail Airline Reservation System

This project is a browser-based proof-of-concept airline reservation system for World Plane Inc. The application allows users to search for one-way or round-trip flights, view possible itineraries, select seats, and create reservations through a graphical user interface.

The system is built with a React frontend, a .NET backend API, and a MySQL database.

---

## Project Features

- Browser-based graphical user interface
- Flight search by departure airport, arrival airport, date, and time window
- One-way and round-trip flight support
- Connecting flight routing with layover validation
- Sorting by departure time, arrival time, and total travel time
- Seat selection and reservation
- Reservation confirmation code generation
- MySQL database support for flight and reservation data

---



## Database Setup

Open MySQL Workbench and create or use the database:

```sql
CREATE DATABASE IF NOT EXISTS flightdata;
USE flightdata;
```

Import the SQL files in this order:

```text
1. flightdata_deltas.sql
2. flightdata_southwests.sql
3. init_db.sql
```

If the project includes separate table creation scripts for reservations or seat inventory, run them after importing the flight data.

After importing, check the tables:

```sql
USE flightdata;

SHOW TABLES;
```

The database should include tables similar to:

```text
airports
connection_rules
deltas
southwests
reservations
reservation_flights
seat_inventory
```

Check whether the data was imported successfully:

```sql
SELECT COUNT(*) FROM deltas;
SELECT COUNT(*) FROM southwests;
SELECT COUNT(*) FROM airports;
SELECT COUNT(*) FROM connection_rules;
SELECT COUNT(*) FROM seat_inventory;
```

---

## Backend Setup

Open a terminal and go to the backend API folder:

```bash
cd backend/api
```

Restore backend dependencies:

```bash
dotnet restore
```

Run the backend:

```bash
dotnet run
```

If the backend starts successfully, you should see something like:

```text
Now listening on: http://localhost:5000
Application started.
```

You can test the backend API by opening:

```text
http://localhost:5000/swagger
```

---

## Backend Database Configuration

The database connection string is stored in:

```text
backend/api/appsettings.json
```

Example configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;uid=root;pwd=YOUR_PASSWORD;database=flightdata"
  }
}
```

If your MySQL root account does not have a password, use:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;uid=root;pwd=;database=flightdata"
  }
}
```

Make sure the database name matches your MySQL database name. In this project, the database name is:

```text
flightdata
```

---

## Frontend Setup

Open a second terminal and go to the frontend folder:

```bash
cd frontend
```

Install frontend dependencies:

```bash
npm install
```

If PowerShell blocks npm scripts, use:

```bash
npm.cmd install
```

Run the frontend:

```bash
npm run dev
```

Or, if using PowerShell:

```bash
npm.cmd run dev
```

If the frontend starts successfully, you should see something like:

```text
Local: http://localhost:5173/
```

Open the frontend in your browser:

```text
http://localhost:5173/
```

---

## Frontend API Configuration

The frontend API base URL is defined in:

```text
frontend/src/app/utils/api.ts
```

Make sure it matches the backend URL.

For example, if the backend is running on port 5000:

```ts
export const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000/api/v1';
```

If the backend runs on another port, update this value accordingly.

---

## How to Run the Full Project

Run the project in this order:

1. Start MySQL Server.
2. Make sure the `flightdata` database is imported.
3. Start the backend:

```bash
cd backend/api
dotnet run
```

4. Start the frontend in another terminal:

```bash
cd frontend
npm run dev
```

5. Open the browser:

```text
http://localhost:5173/
```

---
