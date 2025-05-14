# Getting Started with TransactionService

This guide will help you set up and run the TransactionService locally.

## Prerequisites

- .NET SDK (8.0 or later)
- MySQL Database
- RabbitMQ
- Redis (for caching)
- Docker (optional)

## Installation

### 1. Clone the Repository

```bash
git clone https://your-repository-url/DLS_Banking_App.git
cd DLS_Banking_App/Microservices/TransactionService
```

### 2. Configure Database Connection

Update the connection string in appsettings.json:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=transaction_db;User=root;Password=yourpassword;"
  }
}
```

### 3. Configure External Services

Set up service connections in appsettings.json:

```json
{
  "ExternalServices": {
    "UserAccountService": "http://localhost:5001",
    "FraudDetectionService": "http://localhost:5002"
  }
}
```

### 4. .NET 9.0 Issue

The error you're encountering is because your project is targeting .NET 9.0, but you only have .NET 8.0 SDK installed. You have two options:

1. **Recommended for DocFx**: Temporarily modify the project files to target .NET 8.0 just for documentation generation:
   - Edit `TransactionService.csproj` and `TransactionService.Tests.csproj`
   - Change `<TargetFramework>net9.0</TargetFramework>` to `<TargetFramework>net8.0</TargetFramework>`
   - Generate documentation
   - Change back when done

2. Or install the .NET 9.0 SDK (if available)

## Steps to Generate Documentation

1. Temporarily update the TargetFramework in your project files to net8.0
2. Make the changes to the configuration files as suggested above
3. Run the docfx command:

```bash
cd docs/TransactionService
docfx docfx.json
```

### 5. Run Database Migrations

```bash
dotnet ef database update
```

### 6. Build and Run

```bash
dotnet build
dotnet run
```

The service will be available at http://localhost:5000 by default.

### Docker Deployment

You can also run the service using Docker:

```bash
docker build -t transaction-service .
docker run -p 5000:80 transaction-service
```

### API Endpoints

Once running, the following endpoints will be available:

- POST /api/transactions - Create a new transaction
- GET /api/transactions/{id} - Get transaction by ID
- GET /api/transactions/user/{userId} - Get transactions for a user
- GET /health - Service health check
