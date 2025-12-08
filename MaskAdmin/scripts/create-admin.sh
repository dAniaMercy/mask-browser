#!/bin/bash

# Script to create admin user via API endpoint
# Usage: ./create-admin.sh [password]

PASSWORD=${1:-"Admin123!"}
BASE_URL=${2:-"http://localhost:5000"}

echo "Creating admin user with password: $PASSWORD"
echo "API Base URL: $BASE_URL"
echo ""

# Create admin via API endpoint
response=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/create-admin" \
  -H "Content-Type: application/json" \
  -d "{\"password\": \"$PASSWORD\"}")

# Extract response body and status code
http_code=$(echo "$response" | tail -n1)
body=$(echo "$response" | sed '$d')

echo "HTTP Status: $http_code"
echo "Response: $body"
echo ""

if [ "$http_code" = "200" ]; then
    echo "✓ Admin user created successfully!"
    echo ""
    echo "Login credentials:"
    echo "  Username: admin"
    echo "  Password: $PASSWORD"
    echo "  URL: $BASE_URL/Auth/Login"
else
    echo "✗ Failed to create admin user"
    echo "Please check the error message above"
fi
