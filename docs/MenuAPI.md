# Menu API — CoffeeNChill

## Overview

The Menu API is a RESTful service built using **Azure Functions (.NET 8 Isolated)** and **Azure Table Storage**. It provides full CRUD (Create, Read, Update, Delete) operations for managing CoffeeNChill menu items.

**Author:** Neha Heeralal ST10478910

---

## Architecture

The API is split across three layers:

| File | Purpose |
|------|---------|
| `Models/MenuItem.cs` | Data entity that maps to Azure Table Storage rows |
| `Services/MenuStorageService.cs` | Data access layer — handles all database operations |
| `Functions/MenuFunctions.cs` | API controller — exposes HTTP endpoints and handles validation |

### Data Model

Each menu item uses a **composite key**:

- **PartitionKey** = Category (e.g., "Drinks")
- **RowKey** = SKU (e.g., "COF-001")

Additional fields: `Name`, `Description`, `Price`, `IsAvailable`.

---

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/health` | Health check — verifies the API is running |
| `POST` | `/api/menu` | Create a new menu item |
| `GET` | `/api/menu` | Retrieve all menu items |
| `GET` | `/api/menu/{category}/{sku}` | Retrieve a single item by composite key |
| `GET` | `/api/categories/{category}` | Retrieve all items in a specific category |
| `PUT` | `/api/menu/{category}/{sku}` | Update an existing item (partial update) |
| `DELETE` | `/api/menu/{category}/{sku}` | Delete an item |

---

## Example Requests

### Create a Menu Item

```http
POST /api/menu
Content-Type: application/json

{
  "category": "Drinks",
  "sku": "COF-001",
  "name": "Hot Coffee",
  "description": "Freshly brewed dark roast",
  "price": 3.5,
  "isAvailable": true
}
```

**Response:** `201 Created`

### Get by Category

```http
GET /api/categories/Drinks
```

**Response:** `200 OK` — JSON array of all Drinks items.

### Update an Item

```http
PUT /api/menu/Drinks/COF-001
Content-Type: application/json

{
  "name": "Extra Hot Coffee",
  "price": 4.5,
  "isAvailable": true
}
```

**Response:** `200 OK` — updated item.

---

## Validation Rules

- **Category** and **SKU** are required when creating items.
- **SKU** must match the format `XXX-000` (three letters, dash, three digits). Example: `COF-001`.
- Duplicate SKUs in the same category return `400 Bad Request`.

---

## Routing Note

The category endpoint lives at `/api/categories/{category}` — **not** under `/api/menu/`. This is intentional: Azure Functions would otherwise confuse `/api/menu/category/{category}` with `/api/menu/{category}/{sku}` and route the request to the wrong function.

---

## Running Locally

1. Ensure **Azurite** (Azure Storage Emulator) is running.
2. From the `CoffeeNChill.Functions` directory, run:
   ```
   func start
   ```
3. The API will be available at `http://localhost:7071/api/...`.

---

## Testing

A Postman collection with all endpoints and test scripts is available in `docs/postman/`.

---

## Technology Stack

- **.NET 8** (Isolated worker model)
- **Azure Functions v4**
- **Azure Table Storage** (`Azure.Data.Tables`)
- **Application Insights** (via `Microsoft.ApplicationInsights.WorkerService`)
- **System.Text.Json** for serialization