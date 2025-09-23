# Online Store - Backend Services

## Overview

This project implements two independent microservices for an online store:

- **Auth Service** - JWT token generation for authentication
- **ProductCatalog Service** - Product management and inventory
- **Orders Service** - Order processing and management

## Architecture

```
┌─────────────────┐    ┌──────────────────────┐    ┌─────────────────┐
│   Auth Service  │    │ ProductCatalog API   │    │   Orders API    │
│   Port: 5000    │◄───│     Port: 5001       │◄───│   Port: 5002    │
└─────────────────┘    └──────────────────────┘    └─────────────────┘
                            ▲                            │
                            └────────────────────────────┘
                         Internal Service Auth (JWT)
```

### Internal Authentication Flow

The Orders API communicates with the ProductCatalog API using service-to-service authentication:

1. **Orders API** requests a service token from **Auth Service** using:

   - Client ID: `Orders`
   - Client Secret: Configured in `ServiceAuth` settings
   - Target Audience: `ProductCatalog`

2. **Auth Service** validates the credentials and issues a JWT token with:

   - `client_id` claim: `Orders`
   - `aud` (audience): `ProductCatalog`

3. **Orders API** uses this token in the `Authorization: Bearer` header when calling ProductCatalog API endpoints

4. **ProductCatalog API** validates the token and checks:
   - Token is valid and from trusted issuer
   - Audience matches `ProductCatalog`
   - Client ID is in the allowed list (`Orders`)

Protected endpoints in ProductCatalog API (used by Orders):

- `POST /api/products/validate` - Validate product availability
- `POST /api/products/stock/decrement-batch` - Reserve stock for orders
- `POST /api/products/stock/replenish-batch` - Release stock on order cancellation

## Prerequisites

- .NET 8.0 SDK
- SQL Server
- .NET IDE

## Quick Start

### 1. Database Setup

The services use Entity Framework with automatic migrations. Databases will be created automatically on first run.

**Important**: Update the connection strings in the `appsettings.Development.json` files if you're not using the default SQL Server LocalDB:

- `src/ProductCatalog/ProductCatalog.Api/appsettings.Development.json`
- `src/Orders/Orders.Api/appsettings.Development.json`

**Default connection string:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost; Database=DB_YourDB; Integrated Security=True; TrustServerCertificate=True;"
  }
}
```

### 2. Start Services

Run all three services (order matters for inter-service communication):

```bash
# Terminal 1 - Auth Service
cd src/Auth/Auth.Api
dotnet run

# Terminal 2 - ProductCatalog Service
cd src/ProductCatalog/ProductCatalog.Api
dotnet run

# Terminal 3 - Orders Service
cd src/Orders/Orders.Api
dotnet run
```

### 3. Access Swagger Documentation

- Auth Service: http://localhost:5000/swagger
- ProductCatalog Service: http://localhost:5001/swagger
- Orders Service: http://localhost:5002/swagger

## Authentication & Authorization

### JWT Token Generation

To test the APIs, you need JWT tokens. Use the Auth service:

#### Get Admin Token

```bash
POST http://localhost:5000/Auth/token?role=admin&name=TestAdmin
```

#### Get User Token

```bash
POST http://localhost:5000/Auth/token?role=user&name=TestUser
```

**Response Example:**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "role": "Admin"
}
```

Use the token in subsequent requests:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

## API Examples

### ProductCatalog Service (Port 5001)

#### 1. Create Product (Admin Only)

```bash
POST http://localhost:5001/api/products
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "name": "Gaming Laptop",
  "sku": "LAPTOP-001",
  "price": 1299.99,
  "stockQuantity": 50,
  "isActive": true
}
```

**Response:**

```json
{
  "id": 1,
  "name": "Gaming Laptop",
  "sku": "LAPTOP-001",
  "price": 1299.99,
  "stockQuantity": 50,
  "isActive": true
}
```

#### 2. Get Products List (User/Admin)

```bash
GET http://localhost:5001/api/products?page=1&pageSize=10
Authorization: Bearer {user_or_admin_token}
```

**Response:**

```json
{
  "items": [
    {
      "id": 1,
      "name": "Gaming Laptop",
      "sku": "LAPTOP-001",
      "price": 1299.99,
      "stockQuantity": 50,
      "isActive": true
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

#### 3. Get Product Details (User/Admin)

```bash
GET http://localhost:5001/api/products/1
Authorization: Bearer {user_or_admin_token}
```

#### 4. Update Product (Admin Only)

```bash
PATCH http://localhost:5001/api/products/1
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "price": 1199.99,
  "stockQuantity": 45,
  "isActive": true
}
```

### Orders Service (Port 5002)

#### 1. Create Order (User Only)

```bash
POST http://localhost:5002/api/orders
Authorization: Bearer {user_token}
Idempotency-Key: order-123-456
Content-Type: application/json

[
  {
    "productId": 1,
    "quantity": 2
  }
]
```

**Response:**

```json
{
  "guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "userId": "2",
  "status": "Pending",
  "totalAmount": 2599.98,
  "createdAt": "2025-09-22T10:30:00Z",
  "items": [
    {
      "productId": 1,
      "productName": "Gaming Laptop",
      "productSku": "LAPTOP-001",
      "quantity": 2,
      "unitPrice": 1299.99,
      "totalPrice": 2599.98
    }
  ]
}
```

#### 2. Get Order Details (User Only)

```bash
GET http://localhost:5002/api/orders/a1b2c3d4-e5f6-7890-abcd-ef1234567890
Authorization: Bearer {user_token}
```

#### 3. Get User's Orders (User Only)

```bash
GET http://localhost:5002/api/orders/by-user
Authorization: Bearer {user_token}
```

**Response:**

```json
[
  {
    "guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "status": "Pending",
    "totalAmount": 2599.98,
    "createdAt": "2025-09-22T10:30:00Z",
    "itemsCount": 1
  }
]
```

#### 4. Cancel Order (User Only)

```bash
POST http://localhost:5002/api/orders/a1b2c3d4-e5f6-7890-abcd-ef1234567890/cancel
Authorization: Bearer {user_token}
```

## Order Statuses

- **Pending** - Awaiting confirmation (can be cancelled)
- **Confirmed** - Order confirmed by store
- **Rejected** - Rejected by store
- **Cancelled** - Cancelled by user

## Project Structure

```
src/
├── Auth/Auth.Api/              # JWT token generation
├── ProductCatalog/
│   └── ProductCatalog.Api/     # Product management
└── Orders/
    └── Orders.Api/             # Order processing

tests/                          # Unit and integration tests
```

## Technologies Used

- **Framework**: ASP.NET Core 8.0
- **Database**: Entity Framework Core + SQL Server
- **Authentication**: JWT tokens
- **API Documentation**: Swagger/OpenAPI
- **Validation**: Data Annotations
- **Resilience**: Polly for retry policies

---
