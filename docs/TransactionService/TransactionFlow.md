# Transaction Flow

## Overview
The TransactionService follows a well-defined flow when processing financial transactions. This document outlines the step-by-step process of how a transaction is handled from initiation to completion.

## Transaction Processing Stages

### 1. Request Validation
When a transfer request is received, the `TransactionValidator` first validates:
- Request structure and data integrity
- User permissions and account ownership
- Sufficient funds in the source account

### 2. External Services Health Check
Before proceeding, the service checks if required external services are available:
```csharp
private static async Task CheckExternalServicesHealthAsync(
    ILogger<TransactionService> logger,
    IFraudDetectionService fraudDetectionService,
    TransactionValidator validator,
    Counter errorsTotal)
{
    // If services are down, a ServiceUnavailableException is thrown
}
```

### 3. Create Pending Transaction
A transaction record is created with "pending" status:
```csharp
private static async Task<Transaction> CreatePendingTransactionAsync(
    TransactionRequest request,
    Account fromAccount,
    Account toAccount,
    ILogger<TransactionService> logger,
    ITransactionRepository repository)
{
    // Creates a transaction with pending status
}
```

4. Fraud Detection
The transaction is sent to the FraudDetectionService via RabbitMQ for analysis.

5. Account Updates
Upon successful fraud check, the service:

Creates withdrawal transaction for the source account
Creates deposit transaction for the destination account
Updates account balances via UserAccountService

6. Finalize Transaction
The transaction status is updated to "completed" if successful:
```csharp
private static async Task UpdateTransactionStatusesAsync(
    Transaction transaction,
    Transaction withdrawalTransaction,
    Transaction depositTransaction,
    ITransactionRepository repository)
{
    await repository.UpdateTransactionStatusAsync(transaction.Id, "completed");
    // Updates child transactions as well
}
```

7. Error Handling
If any step fails, the transaction is marked as "failed":
```csharp
private static async Task HandleTransactionFailureAsync(
    Transaction transaction,
    Exception ex,
    ILogger<TransactionService> logger,
    ITransactionRepository repository,
    Counter errorsTotal)
{
    // Records error and updates transaction status
}
```

Transaction Response
Upon completion, a TransactionResponse object is returned to the client containing details about the transaction including its status and IDs.

Monitoring
All transaction stages are monitored using Prometheus metrics:

Request counts
Success/failure counts
Processing time histogram

# Setup & Configuration

## Prerequisites
- .NET 9.0 SDK
- MySQL Database
- RabbitMQ
- Redis (for fraud detection integration)

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
  },
  // Other configuration...
}
```

### 3. Configure Authentication
Set up JWT authentication parameters in appsettings.json:
```json
{
  "Jwt": {
    "Issuer": "your-issuer",
    "Audience": "your-audience",
    "Key": "your-secret-key-with-at-least-16-characters"
  }
}
```

### 4. Configure RabbitMQ
Update RabbitMQ connection in appsettings.json:
```json
{
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "guest",
    "Password": "guest"
  }
}
```
5. Run Database Migrations
dotnet ef migrations add InitialCreate
dotnet ef database update

6. Build and Run
dotnet build
dotnet run

he service will be available at http://localhost:5000 by default.

Docker Deployment
You can also run the service using Docker:
docker build -t transaction-service .
docker run -p 5000:80 -p 9091:9091 transaction-service

Using docker-compose:

docker-compose -f docker-compose.dev.yml up transaction-service

