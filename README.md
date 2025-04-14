## Architecture Diagram Description

### Components
1. **Frontend (React)**:
   - Communicates with:
     - UserAccountService via REST API (e.g., `/users/register`, `/token` for login).
     - QueryService via REST API (e.g., `/transactions?userId=user1`) or GraphQL (e.g., `/graphql`).
2. **Microservices**:
   - **UserAccountService (C#)**: Handles user registration, login, account management (balances), issues JWT tokens, uses MySQL.
   - **Transaction Service (C#)**: Processes transfers, uses MySQL, integrates with Fraud Detection Service via RabbitMQ.
   - **Fraud Detection Service (Python with FastAPI)**: Analyzes transfers for fraud, stores transaction data in Redis, logs errors to a file.
   - **QueryService (C#)**: Handles read operations (CQRS query side), stores denormalized data in Elasticsearch, provides a unified API for the frontend.
3. **Message Queue (RabbitMQ)**:
   - Facilitates async communication between Transaction Service, Fraud Detection Service, and QueryService.
4. **Databases**:
   - **MySQL**:
     - `useraccount_db`: Used by UserAccountService for user and account data.
     - `transaction_db`: Used by Transaction Service for transaction data.
   - **Redis**: Used by Fraud Detection Service for idempotence and transaction storage.
   - **Elasticsearch**: Used by QueryService for denormalized read-optimized data.
5. **Logging**:
   - File-based logging for Fraud Detection Service (errors to `fraud.log`, operational info to console).
   - Planned file-based logging for other services.
6. **Monitoring**:
   - Prometheus and Grafana for monitoring metrics (already implemented in Fraud Detection Service, planned for other services).

### Flow
- Arrows represent communication:
  - **Solid lines**: HTTP (REST/GraphQL) between frontend and microservices.
  - **Dashed lines**: RabbitMQ messages between microservices.
  - **Dotted lines**: Database connections from services to their respective databases.

### Explanation
- **Frontend**: Connects to UserAccountService for user/account operations (via REST) and QueryService for viewing data (via REST/GraphQL).
- **UserAccountService**: Handles user registration, login, and account management via REST API, issues JWT tokens, connects to `useraccount_db` (MySQL), publishes events (`UserCreated`, `AccountCreated`, `BalanceUpdated`) to RabbitMQ.
- **Transaction Service**: Processes transfers via REST API, connects to `transaction_db` (MySQL), sends fraud check requests to `CheckFraud` queue, consumes results from `TransactionServiceQueue`, publishes events (`TransactionCreated`, `TransactionStatusUpdated`) to RabbitMQ.
- **Fraud Detection Service**: Consumes `CheckFraud` messages, analyzes transfers, stores transaction data in Redis, publishes results to `TransactionServiceQueue` and `FraudEvents`, logs errors to a file.
- **QueryService**: Consumes events from all services (`UserCreated`, `AccountCreated`, `BalanceUpdated`, `TransactionCreated`, `FraudDetected`), stores denormalized data in Elasticsearch, provides a REST/GraphQL API for the frontend.
- **RabbitMQ**: Central hub for async messaging between Transaction Service, Fraud Detection Service, and QueryService.
- **Databases**:
  - MySQL: Separate instances for UserAccountService (`useraccount_db`) and Transaction Service (`transaction_db`).
  - Redis: Used by Fraud Detection Service for idempotence and transaction storage.
  - Elasticsearch: Used by QueryService for read-optimized data.
- **Monitoring**: Prometheus scrapes metrics from all services; Grafana visualizes metrics (e.g., fraud detections, transaction rates).

### Detailed Flow (Transfer Example)
1. **Frontend → UserAccountService**: User logs in via REST POST `/token {username, password}`, receives a JWT token.
2. **Frontend → Transaction Service**: REST POST `/transfer {fromAccount, toAccount, amount}` with JWT token in the Authorization header.
3. **Transaction Service → MySQL**: Stores the transaction in `transaction_db` with status `pending`.
4. **Transaction Service → RabbitMQ**: Publishes `"CheckFraud": {transferId, amount, jwtToken}`.
5. **RabbitMQ → Fraud Detection Service**: Consumes `"CheckFraud"`, verifies the JWT token.
6. **Fraud Detection Service → Redis**: Checks for duplicate `transferId`, stores transaction data.
7. **Fraud Detection Service**: Flags if `amount > 1000`, publishes `"FraudResult": {transferId, isFraud, status}` to `TransactionServiceQueue` and `"FraudDetected"` event to `FraudEvents`.
8. **RabbitMQ → Transaction Service**: Consumes `"FraudResult"`, updates transaction status in `transaction_db` (e.g., `approved` or `declined`).
9. **Transaction Service → RabbitMQ**: Publishes `"TransactionStatusUpdated"` event.
10. **RabbitMQ → QueryService**: Consumes events (`TransactionCreated`, `FraudDetected`, `TransactionStatusUpdated`), updates Elasticsearch.
11. **Frontend → QueryService**: Polls REST GET `/transactions?transferId={transferId}` to display status.

### Frontend
- **Technology**: React
- **Functionality**:
  - Register/login via UserAccountService (REST).
  - View account balance and transaction history via QueryService (REST/GraphQL).
  - Initiate a transfer via Transaction Service (REST).
  - Display transfer status (success or flagged as fraud).
- **Scope**: Minimal UI—just a few pages with basic forms and tables, no styling or real-time updates.

### Infrastructure
- **Message Queue**: RabbitMQ
  - Used for async communication (e.g., Transaction → Fraud Detection → QueryService).
  - Simple queues with no advanced features like retries or dead-letter queues.
- **Containerization**: Docker
  - Basic Dockerfiles for each microservice and frontend.
- **Orchestration**: Docker Compose for local development; Kubernetes deployment strategy to be described in the report.
- **Logging**:
  - File-based logging per service (e.g., `fraud.log` for Fraud Detection Service), chosen for simplicity in a demo context.
- **Monitoring**:
  - Prometheus and Grafana for metrics visualization (e.g., fraud detections, transaction rates).
- **CI/CD**:
  - GitHub Actions pipeline: build Docker images, run basic tests, deploy to a cloud provider (or describe the strategy in the report).

### Specifications
#### Functional Requirements
1. **User Management**:
   - Register with username/password.
   - Log in to access the app (JWT authentication).
2. **Account Management**:
   - Create a single account per user with an initial balance.
   - View account balance.
3. **Transaction Processing**:
   - Transfer money between accounts.
   - Display transfer status (success or flagged as fraud).
4. **Fraud Detection**:
   - Flag transfers exceeding $1000 and log the decision.
5. **Querying**:
   - View transaction history and fraud reports.

#### Non-Functional Requirements
- **Scalability**: Microservices structure with separate databases; QueryService with Elasticsearch for scalable reads.
- **Security**: JWT authentication across all services, role-based authorization (e.g., `user`, `admin` roles).
- **Reliability**: Basic error handling; idempotence with Redis to handle retries.
- **Performance**: Adequate for a demo with a few users; Elasticsearch for fast queries.
- **Maintainability**: Simple code with comments, separate services for clear responsibilities.
- **Deployability**: Deployable via Docker Compose locally; CI/CD pipeline for cloud deployment (or described in the report).

### System Architecture Design
- **Architectural Pattern**: Message-Driven Microservices Architecture using RabbitMQ for asynchronous communication, CQRS for separating reads and writes.
- **Team Responsibilities**:
  - Daniel: Frontend + QueryService.
  - Jakob: UserAccountService + RabbitMQ.
  - Albert: Transaction Service.
  - Frederik: Fraud Detection Service + DevOps (CI/CD, monitoring).
  - 
---

## Strategy to Move Forward
1. **Complete Fraud Detection Service**:
   - Enhance fraud detection logic with additional rules.
   - Add email notifications via a `NotificationQueue`.
   - Improve monitoring with more metrics and Grafana dashboards.
   - Document the service (README, Swagger).
2. **Develop UserAccountService (C#)**:
   - Combine user and account management.
   - Implement JWT authentication/authorization with role-based access.
   - Use REST and GraphQL for frontend communication.
   - Publish events for the QueryService.
   - Use a separate MySQL database (`useraccount_db`).
3. **Develop Transaction Service (C#)**:
   - Handle transfers (command side of CQRS).
   - Store transactions in MySQL (`transaction_db`).
   - Integrate with Fraud Detection Service via RabbitMQ.
   - Implement the Saga pattern for distributed transactions.
   - Ensure idempotence with Redis.
   - Add JWT authentication/authorization.
4. **Develop QueryService (C#)**:
   - Handle read operations (query side of CQRS).
   - Use Elasticsearch to store denormalized data.
   - Expose a REST/GraphQL API for the frontend.
   - Add JWT authentication/authorization.
5. **Additional Services**:
   - Notification Service for email notifications.
   - Admin Service with a React frontend for admin operations.
6. **CI/CD and Deployment**:
   - Set up a GitHub Actions pipeline for each service.
   - Describe a cloud deployment strategy in the report (e.g., Kubernetes, geo-replication).
7. **Documentation**:
   - Document each service in GitHub READMEs.
   - Use Swagger for REST APIs and document GraphQL endpoints.
   - Write the final report with architecture diagrams and pattern explanations.

---

## Alignment with Project Requirements
- **Minimum 4 Microservices**: Met with UserAccountService, Transaction Service, Fraud Detection Service, and QueryService.
- **Frontend**: React (Admin Service or main frontend).
- **REST + GraphQL**: REST in Transaction Service and Fraud Detection Service, GraphQL in UserAccountService or QueryService.
- **Message Queues**: RabbitMQ for asynchronous communication.
- **CQRS**: Implemented with Transaction Service (command) and QueryService (query) using Elasticsearch.
- **Idempotence**: Implemented in Fraud Detection Service; planned for Transaction Service.
- **Saga Pattern**: Planned for Transaction Service.
- **Authentication/Authorization**: Implemented in Fraud Detection Service; planned for UserAccountService (centralized) and other services.
- **Monitoring/Logging**: Implemented with Prometheus and Grafana; planned for other services.
- **Email Service**: Planned with Notification Service.
- **CI/CD**: Planned with GitHub Actions.
- **Cloud Deployment**: Strategy to be described in the report.