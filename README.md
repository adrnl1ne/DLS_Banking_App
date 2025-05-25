# DLS Banking Application

## Overview


## How to run the application using Docker Compose.
### 1. Clone the repository:
```bash
git clone https://github.com/adrnl1ne/DLS_Banking_App.git
cd DLS_Banking_App
```

### 2. Create .env file and place in root of project:

```
# Frontend
VITE_API_URL=http://localhost:3001
VITE_USER_SERVICE_URL=http://localhost:5000
VITE_QUERY_SERVICE_URL=http://localhost:4000

# Grafana credentials
GF_SECURITY_ADMIN_USER=admin
GF_SECURITY_ADMIN_PASSWORD=admin

# RabbitMQ credentials
RABBITMQ_DEFAULT_USER=guest
RABBITMQ_DEFAULT_PASS=guest

# MySQL credentials for UserAccountService
MYSQL_PASSWORD_UA=password1!
MYSQL_DATABASE_UA=useraccount_db

# MySQL credentials for TransactionService
MYSQL_PASSWORD_TA=password1!
MYSQL_DATABASE_TA=transaction_db

# MySQL user credentials
MYSQL_USER=root
MYSQL_PASSWORD=password1!

# JWT configuration
JWT_ISSUER=BankingApp
JWT_AUDIENCE=UserAccountAPI
JWT_KEY=XXX

# Prometheus port for services
PROMETHEUS_PORT=9091

# Service token for TransactionService
TRANSACTION_SERVICE_TOKEN=XXX

# Service token for UserAccountService
QUERY_SERVICE_TOKEN=XXX
```

### 3. Run the application:
```bash
docker-compose -f docker-compose.dev.yml up -d --build
```

To run production version:
```bash
docker-compose -f docker-compose.prod.yml up -d --build
```

### 4. Access the application:
- Frontend: http://localhost:3001
- UserAccountService: http://localhost:8002
- TransactionService: http://localhost:8003
- RabbitMQ: http://localhost:15672 (guest/guest)
- Grafana: http://localhost:3000 (admin/admin)

Ports for services:
- UserAccountService: 8002
- TransactionService: 8003
- RabbitMQ: 15672
- Grafana: 3000
- Prometheus: 9090
- Frontend: 3001
- Redis: 6379
- UserAccount MySQL: 3307
- Transaction MySQL: 3308 
- ElasticSearch: 9200

### To stop the application:
```bash
docker compose -f docker-compose.dev.yml down 
```

### To run using Kubernetes create a secrets.yaml file with the following content:
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: banking-app-secret
  namespace: default
type: Opaque
stringData:
  GF_SECURITY_ADMIN_USER: "admin"
  GF_SECURITY_ADMIN_PASSWORD: "admin"
  RABBITMQ_DEFAULT_USER: "guest"
  RABBITMQ_DEFAULT_PASS: "guest"
  MYSQL_PASSWORD_UA: "password1!"
  MYSQL_PASSWORD_TA: "password1!"
  MYSQL_USER: "root"
  MYSQL_PASSWORD: "password1!"
  JWT_KEY: "XXX"
  TRANSACTION_SERVICE_TOKEN: "XXX"
```

