# QueryService Documentation

## Overview

The **QueryService** is a read-optimized microservice in the DLS Banking App architecture. It implements the Query side of the CQRS pattern, providing fast, flexible, and scalable access to banking data via GraphQL endpoints. The service synchronizes its data from events published by other microservices (such as UserAccountService and TransactionService) through RabbitMQ, and stores the data in Elasticsearch indices.

---

## Architecture

- **CQRS:** QueryService is responsible only for queries (reads), not for commands (writes).
- **Event-Driven:** Listens to RabbitMQ events (`AccountEvents`, `TransactionCreated`, `FraudEvents`) and updates Elasticsearch indices accordingly.
- **Elasticsearch:** All data is stored in Elasticsearch for fast, flexible querying.
- **GraphQL API:** Exposes a GraphQL endpoint for clients to query data.

---

## Main Features

- **Account Queries:** Get all accounts, account history, and deleted accounts.
- **Transaction Queries:** Get transaction history, filter by account.
- **Fraud Queries:** Get fraud check results for transfers.
- **Immutable Data:** Implements tombstone and snapshot patterns for auditability and consistency.

---

## Event Integration

### Consumed RabbitMQ Queues

- **AccountEvents:** Handles account creation, deletion, renaming, and balance updates.
- **TransactionCreated:** Handles new transaction events.
- **FraudEvents:** Handles fraud check results.

### Event Processing

- Each event is deserialized and processed by a handler in `RabbitMQListener.cs`.
- Events update the appropriate Elasticsearch index:
  - `accounts` (current state of accounts)
  - `account_events` (account event history)
  - `transaction_history` (all transactions)
  - `fraud` (fraud check results)
  - `deleted_accounts` (tombstone for deleted accounts)

---

## Elasticsearch Indices

| Index Name           | Document Type              | Description                                 |
|----------------------|---------------------------|---------------------------------------------|
| accounts             | AccountEvent              | Current state of all accounts               |
| account_events       | AccountEvent              | Full event history for accounts             |
| transaction_history  | TransactionCreatedEvent   | All transactions                            |
| fraud                | CheckFraudEvent           | Fraud check results                         |
| deleted_accounts     | DeletedAccount            | Tombstone records for deleted accounts      |
| users                | UserDocument              | User data                                   |

Index mappings are managed in [`utils/ES.cs`](utils/ES.cs) and created at startup by [`Helpers.cs`](Helpers.cs).

---

## GraphQL API

### Endpoint

- **URL:** `/graphql`
- **Type:** POST (GraphQL queries)

### Example Queries

#### Get All Accounts

```graphql
query {
  getAccounts {
    accountId
    userId
    name
    amount
    timestamp
  }
}
```

#### Get Account History

```graphql
query {
  getAccountHistory(accountId: 1) {
    eventType
    accountId
    userId
    name
    amount
    transactionId
    transactionType
    timestamp
  }
}
```

#### Get Deleted Accounts

```graphql
query {
  getDeletedAccounts(userId: 1) {
    accountId
    userId
    name
    timestamp
  }
}
```

#### Get Transactions

```graphql
query {
  getTransactions(accountId: "4") {
    transferId
    fromAccount
    toAccount
    amount
    status
    description
    createdAt
  }
}
```

#### Get Fraud Events

```graphql
query {
  getFraudEvents(transferId: "abc-123") {
    transferId
    isFraud
    status
    amount
    timestamp
  }
}
```

---

## Patterns Used

- **CQRS:** Strict separation of read and write models.
- **Immutable Data:** All events are stored; deletions use the tombstone pattern (`deleted_accounts`).
- **Snapshot Pattern:** The `accounts` index holds the latest state (snapshot) of each account.
- **Tombstone Pattern:** Deleted accounts are written to `deleted_accounts` before removal from `accounts`.
- **Idempotence:** Event handlers are designed to be idempotent (safe to process the same event multiple times).
- **Commutative Handlers:** Event order does not affect the final state.
- **Saga Pattern:** Supported via event-driven coordination (e.g., transaction + fraud + account update).
- **Caching:** Elasticsearch acts as a high-performance cache for queries.

---

## Adding New Integrations

- **To add a new event type:**  
  1. Add the event to the appropriate RabbitMQ queue.
  2. Add a handler in `RabbitMQListener.cs`.
  3. Update `utils/ES.cs` and `Helpers.cs` if a new index/document type is needed.
  4. Add a new GraphQL query in `Query.cs` if you want to expose it.

- **To add a new query:**  
  1. Add a method to `Query.cs`.
  2. Register the new type in GraphQL server setup if needed.

---

## Developer Notes

- **Startup:** Indices are auto-created at startup if missing.
- **Error Handling:** All event handlers log errors and continue processing.
- **Extensibility:** Add new document types and indices by updating `utils/ES.cs` and `Helpers.cs`.
- **Testing:** You can use the `/graphql` endpoint with any GraphQL client or the built-in GraphQL Playground.

---

## References

- [`Query.cs`](Query.cs): Main GraphQL query endpoints.
- [`RabbitMQListener.cs`](RabbitMQListener.cs): Event consumers and handlers.
- [`utils/ES.cs`](utils/ES.cs): Index mapping.
- [`Helpers.cs`](Helpers.cs): Index creation and setup.
- [`DTO/`](DTO/): Document and event DTOs.

---

## Contact

For questions or integration help, contact the backend team or check the project README.
