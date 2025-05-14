---
_layout: landing
---

# TransactionService Documentation

## Overview
The TransactionService is a core microservice in the DLS Banking Application, responsible for processing financial transactions such as transfers between accounts.

## Key Features
- Secure transaction processing with fraud detection
- Transaction logging and auditing
- Account balance updates via integration with UserAccountService
- Event-based architecture using RabbitMQ
- Comprehensive error handling and resilience patterns

## Getting Started
- [Setup and Configuration](articles/setup.md)
- [API Documentation](api/index.md)
- [Transaction Flow](articles/transaction-flow.md)

## Architecture
The TransactionService follows a microservice architecture pattern, communicating with other services like UserAccountService and FraudDetectionService through REST APIs and message queues.