# Fraud Detection Service Documentation

## Overview
The Fraud Detection Service is a Python-based microservice built with FastAPI that analyzes banking transactions for potential fraudulent activity. It operates asynchronously within the DLS Banking App ecosystem, processing transactions through RabbitMQ message queues and storing results in Redis.

## Table of Contents
- [Getting Started](getting-started.md)
- [Architecture](architecture.md)
- [API Reference](api-reference.md)
- [Configuration](configuration.md)
- [Monitoring and Logging](monitoring.md)

## Key Features
- Real-time fraud detection for banking transactions
- Asynchronous processing via RabbitMQ
- Redis-based result storage and caching
- JWT token validation
- Prometheus metrics integration
- Structured logging

## Tech Stack
- Python 3.x
- FastAPI
- RabbitMQ (for message queue)
- Redis (for result storage and caching)
- Prometheus (for metrics)
- Docker

## Quick Links
- [API Documentation](api-reference.md)
- [Configuration Guide](configuration.md)
- [Deployment Guide](deployment.md)
- [Monitoring Setup](monitoring.md) 