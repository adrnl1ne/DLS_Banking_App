# RabbitMQ Queue Documentation

This document outlines the RabbitMQ queues used by the UserAccountService, including their names and payload structures.

## Queue: AccountEvents

**Description**: This queue handles events related to account operations, such as account creation, deletion, renaming, and balance updates. These events are published by the `UserAccountService` to notify downstream services, such as the `QueryService`, which updates Elasticsearch for read-optimized queries (CQRS pattern).

### Event: AccountCreated

**Description**: Published when a new account is created.

**Payload**:
```json
{
  "event_type": "AccountCreated",
  "accountId": "<int>",
  "userId": "<int>",
  "name": "<string>",
  "amount": <decimal>,
  "timestamp": "<iso-8601-timestamp>"
}
```

**Field Descriptions**:
- `event_type`: The type of event (`AccountCreated`).
- `accountId`: The unique identifier of the created account.
- `userId`: The ID of the user who owns the account.
- `name`: The name of the account.
- `amount`: The initial balance of the account (decimal, typically 0).
- `timestamp`: The UTC timestamp of when the event occurred, in ISO 8601 format (e.g., `2025-04-25T12:34:56.789Z`).

### Event: AccountDeleted

**Description**: Published when an account is deleted.

**Payload**:
```json
{
  "event_type": "AccountDeleted",
  "accountId": "<int>",
  "userId": "<int>",
  "name": "<string>",
  "timestamp": "<iso-8601-timestamp>"
}
```

**Field Descriptions**:
- `event_type`: The type of event (`AccountDeleted`).
- `accountId`: The unique identifier of the deleted account.
- `userId`: The ID of the user who owned the account.
- `name`: The name of the account (for reference).
- `timestamp`: The UTC timestamp of when the event occurred, in ISO 8601 format.

### Event: AccountRenamed

**Description**: Published when an account’s name is updated.

**Payload**:
```json
{
  "event_type": "AccountRenamed",
  "accountId": "<int>",
  "userId": "<int>",
  "name": "<string>",
  "timestamp": "<iso-8601-timestamp>"
}
```

**Field Descriptions**:
- `event_type`: The type of event (`AccountRenamed`).
- `accountId`: The unique identifier of the account.
- `userId`: The ID of the user who owns the account.
- `name`: The new name of the account.
- `timestamp`: The UTC timestamp of when the event occurred, in ISO 8601 format.

### Event: AccountBalanceUpdated

**Description**: Published when an account’s balance is updated, typically triggered by the `TransactionService` after fraud approval.

**Payload**:
```json
{
  "event_type": "AccountBalanceUpdated",
  "accountId": "<int>",
  "userId": "<int>",
  "amount": <decimal>,
  "transactionId": "<string>",
  "timestamp": "<iso-8601-timestamp>"
}
```

**Field Descriptions**:
- `event_type`: The type of event (`AccountBalanceUpdated`).
- `accountId`: The unique identifier of the account.
- `userId`: The ID of the user who owns the account.
- `amount`: The new balance of the account.
- `transactionId`: The unique identifier of the transaction causing the update, used for idempotence.
- `timestamp`: The UTC timestamp of when the event occurred, in ISO 8601 format.

**Consumer Expectations**:
- The `QueryService` should consume messages from this queue and update Elasticsearch accordingly:
  - `AccountCreated`: Add a new account document.
  - `AccountDeleted`: Remove the account document.
  - `AccountRenamed`: Update the account’s name field.
  - `AccountBalanceUpdated`: Update the account’s balance field.
- Ensure the consumer acknowledges messages (`autoAck: true` or manual acknowledgment) to avoid message loss.
- The queue is non-durable, non-exclusive, and does not auto-delete, as defined in the `RabbitMqEventPublisher`. For production, consider setting `durable: true` to persist the queue across RabbitMQ restarts.
- The `UserAccountService` uses Redis (`account:transaction:{transactionId}` keys, 7-day expiry) to ensure idempotence for balance updates, preventing duplicate processing of the same `transactionId`.

**Integration Notes**:
- The `TransactionService` triggers `AccountBalanceUpdated` events by calling `PUT /api/Account/{id}/balance` after receiving an `approved` fraud check from the `FraudDetectionService`.
- Coordinate with the `QueryService` to ensure it processes these events correctly, maintaining consistency with the MySQL database (`mysql-useraccount`).