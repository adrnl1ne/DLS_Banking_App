#!/bin/bash

# Script to test the banking app API endpoints

echo "🔍 Testing Banking App API Endpoints"
echo "===================================="

# Test credentials (from the database we initialized)
USERNAME="john_doe"
PASSWORD="hashed_password_1"

echo ""
echo "1. Testing User Account Service Health..."
HEALTH_RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" http://localhost:8002/api/health)
echo "$HEALTH_RESPONSE"

echo ""
echo "2. Testing Login Endpoint..."
LOGIN_RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" \
  -X POST \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"$USERNAME\",\"password\":\"$PASSWORD\"}" \
  http://localhost:8002/api/Token/login)
echo "$LOGIN_RESPONSE"

echo ""
echo "3. Testing Transaction Service Health..."
TRANSACTION_HEALTH=$(curl -s -w "\nHTTP_STATUS:%{http_code}" http://localhost:8003/api/health)
echo "$TRANSACTION_HEALTH"

echo ""
echo "4. Testing Query Service Health..."
QUERY_HEALTH=$(curl -s -w "\nHTTP_STATUS:%{http_code}" http://localhost:8004/api/health)
echo "$QUERY_HEALTH"

echo ""
echo "🔄 Testing CORS Headers for login endpoint..."
CORS_TEST=$(curl -s -w "\nHTTP_STATUS:%{http_code}" \
  -H "Origin: http://localhost:3001" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: Content-Type" \
  -X OPTIONS \
  http://localhost:8002/api/Token/login)
echo "$CORS_TEST"

echo ""
echo "✅ API Test Complete!"
