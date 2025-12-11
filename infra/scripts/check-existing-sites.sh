#!/bin/bash

# Script to check existing Nginx configuration before adding MaskBrowser

echo "=== Checking Existing Nginx Configuration ==="
echo ""

echo "1. Existing sites in sites-enabled:"
ls -1 /etc/nginx/sites-enabled/
echo ""

echo "2. Server names configured:"
grep -rh "server_name" /etc/nginx/sites-enabled/ 2>/dev/null | grep -v "#" | sed 's/^[ \t]*//' | sort -u
echo ""

echo "3. Default server directives:"
grep -rn "default_server" /etc/nginx/sites-enabled/ 2>/dev/null || echo "None found"
echo ""

echo "4. Ports being used:"
grep -rh "listen" /etc/nginx/sites-enabled/ 2>/dev/null | grep -v "#" | sed 's/^[ \t]*//' | sort -u
echo ""

echo "5. SSL certificates:"
certbot certificates 2>/dev/null || echo "Certbot not installed or no certificates"
echo ""

echo "6. Nginx test:"
nginx -t
echo ""

echo "=== Analysis ==="
echo ""

# Check for conflicts
if grep -rq "default_server" /etc/nginx/sites-enabled/; then
    echo "⚠️  WARNING: default_server found in existing config"
    echo "   MaskBrowser will NOT use default_server to avoid conflicts"
else
    echo "✓ No default_server conflicts"
fi
echo ""

if grep -rq "server_name.*maskbrowser" /etc/nginx/sites-enabled/; then
    echo "⚠️  WARNING: maskbrowser domain already configured!"
else
    echo "✓ No maskbrowser domain conflicts"
fi
echo ""

echo "=== Ready to proceed with MaskBrowser setup ==="
