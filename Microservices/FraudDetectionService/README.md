## Queue: FraudEvents

**Description**: This queue handles events related to fraud detection outcomes, published by the `FraudDetectionService` to notify the `QueryService`.

### Event: FraudCheckCompleted

**Description**: Published when a fraud check is completed for a transaction.

**Payload**:
```json
{
  "event_type": "FraudCheckCompleted",
  "transferId": "<string>",
  "isFraud": <bool>,
  "status": "approved|declined",
  "amount": <decimal>,
  "timestamp": "<iso-8601-timestamp>"
}