#!/bin/bash

# Script to reset admin password via API endpoint
# Usage: ./reset-password.sh [new_password]

NEW_PASSWORD=${1:-"Admin123!"}
BASE_URL=${2:-"http://localhost:5000"}

echo "Resetting admin password to: $NEW_PASSWORD"
echo "API Base URL: $BASE_URL"
echo ""

# Reset password via API endpoint
response=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/reset-admin-password" \
  -H "Content-Type: application/json" \
  -d "{\"newPassword\": \"$NEW_PASSWORD\"}")

# Extract response body and status code
http_code=$(echo "$response" | tail -n1)
body=$(echo "$response" | sed '$d')

echo "HTTP Status: $http_code"
echo "Response: $body"
echo ""

if [ "$http_code" = "200" ]; then
    echo "✓ Admin password reset successfully!"
    echo ""
    echo "New login credentials:"
    echo "  Username: admin"
    echo "  Password: $NEW_PASSWORD"
    echo "  URL: $BASE_URL/Auth/Login"
else
    echo "✗ Failed to reset admin password"
    echo "Please check the error message above"
fi
