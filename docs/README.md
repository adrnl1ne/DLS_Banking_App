#  Architecture Diagram Description

## Components
1. **Frontend (React)**:
   - Communicates with:
     - `UserAccountService` via REST API (e.g., `/users/register`, `/token` for login).
     - `QueryService` via REST API (e.g., `/transactions?userId=user1`) or GraphQL (e.g., `/graphql`).
2. **Microservices**:
   - **UserAccountService (C#)**: Handles user registration, login, account management (balances), issues JWT tokens, uses MySQL.
   - **Transaction Service (C#)**: Processes transfers, uses MySQL, integrates with `Fraud Detection Service` by publishing to `CheckFraud` queue (RabbitMQ) and polling Redis for results, publishes events to RabbitMQ.
   - **Fraud Detection Service (Python with FastAPI)**: Analyzes transfers for fraud (flags amounts > $1000), stores transaction data and fraud check results in Redis, logs errors to a file, publishes events to RabbitMQ.
   - **QueryService (C#)**: Handles read operations (CQRS query side), stores denormalized data in Elasticsearch, provides a unified API for the frontend.
3. **Message Queue (RabbitMQ)**:
   - Facilitates async communication between `Transaction Service`, `Fraud Detection Service`, and `QueryService`.
   - Queues: `CheckFraud` (for fraud check requests), `FraudResult` (for monitoring), `FraudEvents` (for event publishing).
4. **Databases**:
   - **MySQL**:
     - `useraccount_db`: Used by `UserAccountService` for user and account data.
     - `transaction_db`: Used by `Transaction Service` for transaction data.
   - **Redis**: Used by `Fraud Detection Service` for idempotence, transaction storage, and fraud check result storage; used by `Transaction Service` to retrieve fraud check results.
   - **Elasticsearch**: Used by `QueryService` for denormalized read-optimized data.
5. **Logging**:
   - File-based logging for `Fraud Detection Service` (errors to `fraud.log`, operational info to console).
   - Planned file-based logging for other services.
6. **Monitoring**:
   - Prometheus and Grafana for monitoring metrics (implemented in `Fraud Detection Service`, planned for other services).

### Flow
- Arrows represent communication:
  - **Solid lines**: HTTP (REST/GraphQL) between frontend and microservices; HTTP between `Transaction Service` and Redis (for polling fraud check results).
  - **Dashed lines**: RabbitMQ messages between microservices (e.g., `CheckFraud` queue).
  - **Dotted lines**: Database connections from services to their respective databases (MySQL, Redis, Elasticsearch).

### Explanation
- **Frontend**: Connects to `UserAccountService` for user/account operations (via REST) and `QueryService` for viewing data (via REST/GraphQL).
- **UserAccountService**: Handles user registration, login, and account management via REST API, issues JWT tokens, connects to `useraccount_db` (MySQL), publishes events (`UserCreated`, `AccountCreated`, `BalanceUpdated`) to RabbitMQ.
- **Transaction Service**: Processes transfers via REST API, connects to `transaction_db` (MySQL), sends fraud check requests to `CheckFraud` queue, polls Redis for fraud check results (`fraud:result:{transferId}`), publishes events (`TransactionCreated`, `TransactionStatusUpdated`) to RabbitMQ, throws `ServiceUnavailableException` if services are down.
- **Fraud Detection Service**: Consumes `CheckFraud` messages, analyzes transfers (flags amounts > $1000), stores transaction data and fraud check results in Redis, publishes events to `FraudResult` and `FraudEvents` queues, logs errors to a file.
- **QueryService**: Consumes events from all services (`UserCreated`, `AccountCreated`, `BalanceUpdated`, `TransactionCreated`, `FraudDetected`), stores denormalized data in Elasticsearch, provides a REST/GraphQL API for the frontend.
- **RabbitMQ**: Central hub for async messaging between `Transaction Service`, `Fraud Detection Service`, and `QueryService`.
- **Databases**:
  - MySQL: Separate instances for `UserAccountService` (`useraccount_db`) and `Transaction Service` (`transaction_db`).
  - Redis: Used by `Fraud Detection Service` for idempotence, transaction storage, and fraud check results; used by `Transaction Service` to retrieve fraud check results.
  - Elasticsearch: Used by `QueryService` for read-optimized data.
- **Monitoring**: Prometheus scrapes metrics from all services; Grafana visualizes metrics (e.g., fraud detections, transaction rates).

### Detailed Flow (Transfer Example)
1. **Frontend → `UserAccountService`**: User logs in via REST `POST /token {username, password}`, receives a JWT token.
2. **Frontend → `Transaction Service`**: REST `POST /transfer {fromAccount, toAccount, amount}` with JWT token in the Authorization header.
3. **Transaction Service**: Calls `CheckExternalServicesHealthAsync`, throws `ServiceUnavailableException` (mapped to HTTP 503) if either `UserAccountService` or `FraudDetectionService` is unavailable.
4. **Transaction Service → MySQL**: Stores the transaction in `transaction_db` with status `pending`.
5. **Transaction Service → RabbitMQ**: Publishes `"CheckFraud": {transferId, amount, jwtToken}`.
6. **RabbitMQ → `Fraud Detection Service`**: Consumes `"CheckFraud"`, verifies the JWT token.
7. **Fraud Detection Service → Redis**: Checks for duplicate `transferId`, stores transaction data.
8. **Fraud Detection Service**: Flags if `amount > 1000`, stores result (`isFraud: true`, `status: "declined"`) in Redis (`fraud:result:{transferId}`), publishes `"FraudResult"` (for monitoring) and `"FraudDetected"` event to `FraudEvents`.
9. **Transaction Service → Redis**: Polls for fraud check result (`fraud:result:{transferId}`), retrieves result (`isFraud: true`, `status: "declined"`).
10. **Transaction Service → MySQL**: Updates transaction status in `transaction_db` to `declined`, throws `InvalidOperationException` ("Transaction declined due to potential fraud").
11. **Transaction Service → RabbitMQ**: Publishes `"TransactionStatusUpdated"` event.
12. **RabbitMQ → `QueryService`**: Consumes events (`TransactionCreated`, `FraudDetected`, `TransactionStatusUpdated`), updates Elasticsearch.
13. **Frontend → `QueryService`**: Polls REST `GET /transactions?transferId={transferId}` to display status (`declined`).

---

## Infrastructure
- **Message Queue**: RabbitMQ
  - Used for async communication (e.g., `Transaction Service` → `Fraud Detection Service` → `QueryService`).
  - Queues: `CheckFraud`, `FraudResult`, `FraudEvents` (removed `TransactionServiceQueue` as fraud results are now stored in Redis).
  - Simple queues with no advanced features like retries or dead-letter queues.
- **Containerization**: Docker
  - Basic Dockerfiles for each microservice and frontend.
- **Orchestration**: Docker Compose for local development; Kubernetes deployment strategy to be described in the report.
- **Logging**:
  - File-based logging per service (e.g., `fraud.log` for `Fraud Detection Service`), chosen for simplicity in a demo context.
- **Monitoring**:
  - Prometheus and Grafana for metrics visualization (e.g., fraud detections, transaction rates).
- **CI/CD**:
  - GitHub Actions pipeline: build Docker images, run basic tests, deploy to a cloud provider (or describe the strategy in the report).
