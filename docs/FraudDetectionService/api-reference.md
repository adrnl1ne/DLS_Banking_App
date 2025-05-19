# API Reference

## Message Queue Endpoints

### CheckFraud Queue Consumer

The service primarily operates by consuming messages from the `CheckFraud` RabbitMQ queue.

#### Message Format
```json
{
    "transferId": "string",
    "amount": "decimal",
    "jwtToken": "string"
}
```

#### Response (Published to Redis)
Key format: `fraud:result:{transferId}`
```json
{
    "isFraud": "boolean",
    "status": "string",
    "reason": "string",
    "timestamp": "ISO8601 datetime"
}
```

## HTTP Endpoints

### Health Check
```
GET /health
```

Returns the health status of the service and its dependencies.

#### Response
```json
{
    "status": "string",
    "rabbitmq": "boolean",
    "redis": "boolean",
    "timestamp": "ISO8601 datetime"
}
```

### Metrics
```
GET /metrics
```

Returns Prometheus metrics for the service.

#### Available Metrics
- `fraud_checks_total`: Counter of total fraud checks performed
- `fraud_detections_total`: Counter of detected fraud cases
- `fraud_check_duration_seconds`: Histogram of fraud check duration
- `queue_message_processing_errors_total`: Counter of message processing errors
- `redis_operation_errors_total`: Counter of Redis operation errors

## Event Publishing

### FraudResult Queue
Published when a fraud check is completed.

#### Message Format
```json
{
    "transferId": "string",
    "result": {
        "isFraud": "boolean",
        "status": "string",
        "reason": "string"
    },
    "timestamp": "ISO8601 datetime"
}
```

### FraudEvents Queue
Published for monitoring and auditing purposes.

#### Message Format
```json
{
    "eventType": "string",
    "transferId": "string",
    "details": {
        "amount": "decimal",
        "isFraud": "boolean",
        "reason": "string"
    },
    "timestamp": "ISO8601 datetime"
}
```

## Error Responses

### Common Error Codes
- `INVALID_TOKEN`: JWT token validation failed
- `DUPLICATE_TRANSFER`: Transfer ID already processed
- `REDIS_ERROR`: Redis operation failed
- `QUEUE_ERROR`: RabbitMQ operation failed

### Error Response Format
```json
{
    "error": {
        "code": "string",
        "message": "string",
        "details": "object (optional)"
    },
    "timestamp": "ISO8601 datetime"
}
```

## Rate Limiting

The service implements rate limiting per consumer:
- Maximum 100 messages processed per second per consumer
- Maximum 1000 messages in processing queue per consumer

## Data Retention

- Fraud check results in Redis: 24 hours
- Event logs: 30 days
- Metrics data: 7 days

## Authentication

The service validates JWT tokens issued by the UserAccountService. Tokens must:
- Be valid and not expired
- Contain required claims (userId, permissions)
- Have appropriate permissions for fraud checking 