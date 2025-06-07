#!/bin/bash

# Script to set up all necessary port forwards for the Banking App

echo "🚀 Starting port forwards for Banking App..."

# Kill any existing port forwards
echo "🧹 Cleaning up existing port forwards..."
pkill -f "kubectl port-forward" || true

# Wait a moment for cleanup
sleep 2

echo "📡 Setting up port forwards..."

# Frontend
echo "  → Frontend (3001)"
kubectl port-forward svc/frontend 3001:3001 > /dev/null 2>&1 &
FRONTEND_PID=$!

# User Account Service
echo "  → User Account Service (8002)"
kubectl port-forward svc/user-account-service 8002:8002 > /dev/null 2>&1 &
USER_ACCOUNT_PID=$!

# Transaction Service
echo "  → Transaction Service (8003)"
kubectl port-forward svc/transaction-service 8003:8003 > /dev/null 2>&1 &
TRANSACTION_PID=$!

# Query Service
echo "  → Query Service (8004)"
kubectl port-forward svc/query-service 8004:8004 > /dev/null 2>&1 &
QUERY_PID=$!

# RabbitMQ Management (optional)
echo "  → RabbitMQ Management (15672)"
kubectl port-forward svc/rabbitmq 15672:15672 > /dev/null 2>&1 &
RABBITMQ_PID=$!

# MySQL UserAccount (for debugging)
echo "  → MySQL UserAccount (3307)"
kubectl port-forward svc/mysql-useraccount 3307:3306 > /dev/null 2>&1 &
MYSQL_UA_PID=$!

# MySQL Transaction (for debugging)
echo "  → MySQL Transaction (3308)"
kubectl port-forward svc/mysql-transaction 3308:3306 > /dev/null 2>&1 &
MYSQL_TX_PID=$!

# Wait for port forwards to establish
echo "⏳ Waiting for port forwards to establish..."
sleep 5

# Test connectivity
echo "🔍 Testing connectivity..."
echo "  → Frontend: $(curl -s -o /dev/null -w "%{http_code}" http://localhost:3001 || echo "Failed")"
echo "  → User Account Service: $(curl -s -o /dev/null -w "%{http_code}" http://localhost:8002/api/health || echo "Failed")"
echo "  → Transaction Service: $(curl -s -o /dev/null -w "%{http_code}" http://localhost:8003/api/health || echo "Failed")"
echo "  → Query Service: $(curl -s -o /dev/null -w "%{http_code}" http://localhost:8004/api/health || echo "Failed")"

echo ""
echo "✅ Port forwards are active!"
echo ""
echo "🌐 Access your application at: http://localhost:3001"
echo "📊 RabbitMQ Management: http://localhost:15672"
echo ""
echo "📝 PIDs: Frontend=$FRONTEND_PID, UserAccount=$USER_ACCOUNT_PID, Transaction=$TRANSACTION_PID, Query=$QUERY_PID"
echo ""
echo "To stop all port forwards, run: pkill -f 'kubectl port-forward'"
echo "Or press Ctrl+C to stop this script and all port forwards"

# Keep script running
trap 'echo "🛑 Stopping all port forwards..."; pkill -f "kubectl port-forward"; exit 0' INT

# Wait indefinitely
while true; do
    sleep 10
    # Check if any port forward died and restart if needed
    if ! kill -0 $FRONTEND_PID 2>/dev/null; then
        echo "⚠️  Frontend port forward died, restarting..."
        kubectl port-forward svc/frontend 3001:3001 > /dev/null 2>&1 &
        FRONTEND_PID=$!
    fi
done
