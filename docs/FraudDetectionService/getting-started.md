# Getting Started with Fraud Detection Service

## Prerequisites
- Python 3.8 or higher
- Docker and Docker Compose
- RabbitMQ instance
- Redis instance
- Access to DLS Banking App environment

## Local Development Setup

### 1. Clone the Repository
```bash
git clone <repository-url>
cd DLS_Banking_App/src/FraudDetectionService
```

### 2. Set Up Python Environment
```bash
# Create a virtual environment
python -m venv venv

# Activate the virtual environment
# On Windows:
.\venv\Scripts\activate
# On macOS/Linux:
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt
```

### 3. Configure Environment Variables
Create a `.env` file in the root directory:
```env
# RabbitMQ Configuration
RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest

# Redis Configuration
REDIS_HOST=localhost
REDIS_PORT=6379
REDIS_DB=0

# Service Configuration
FRAUD_THRESHOLD=1000
JWT_SECRET_KEY=your_secret_key
LOG_LEVEL=INFO
```

### 4. Start Required Services
```bash
# Start RabbitMQ and Redis using Docker Compose
docker-compose up -d rabbitmq redis
```

### 5. Run the Service
```bash
# For development
uvicorn main:app --reload --port 8002

# For production
uvicorn main:app --host 0.0.0.0 --port 8002
```

## Docker Deployment

### Build the Image
```bash
docker build -t fraud-detection-service .
```

### Run with Docker
```bash
docker run -d \
  --name fraud-detection-service \
  -p 8002:8002 \
  --env-file .env \
  fraud-detection-service
```

## Testing

### Run Unit Tests
```bash
pytest tests/
```

### Run Integration Tests
```bash
pytest tests/integration/ --integration
```

## Health Check
Once the service is running, you can verify its health at:
```
http://localhost:8002/health
```

## Metrics
Prometheus metrics are available at:
```
http://localhost:8002/metrics
```

## Common Issues and Troubleshooting

### RabbitMQ Connection Issues
- Verify RabbitMQ is running: `docker ps | grep rabbitmq`
- Check RabbitMQ logs: `docker logs rabbitmq`
- Ensure correct credentials in `.env`

### Redis Connection Issues
- Verify Redis is running: `docker ps | grep redis`
- Check Redis logs: `docker logs redis`
- Test Redis connection: `redis-cli ping`

### JWT Validation Failures
- Ensure JWT_SECRET_KEY matches the one used by UserAccountService
- Verify token expiration
- Check token format in requests

## Next Steps
- Review the [API Reference](api-reference.md) for endpoint details
- Set up [Monitoring](monitoring.md) for production
- Configure [Logging](monitoring.md#logging) for better observability 