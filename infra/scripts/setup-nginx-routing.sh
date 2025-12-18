#!/bin/bash

# Скрипт для настройки маршрутизации nginx для трех доменов:
# - wbmoneyback.ru
# - maskbrowser.ru
# - admin.maskbrowser.ru
#
# IP: 109.172.101.73

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1"
    exit 1
}

warn() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    error "Please run as root or with sudo"
fi

log "=== Nginx Routing Setup for Multiple Domains ==="
echo ""

# Step 1: Backup existing configs
log "Step 1: Creating backup of existing configurations..."
BACKUP_DIR="/root/nginx-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# Backup all existing configs
if [ -d "/etc/nginx/sites-enabled" ]; then
    cp -r /etc/nginx/sites-enabled/* "$BACKUP_DIR/" 2>/dev/null || true
    cp -r /etc/nginx/sites-available/* "$BACKUP_DIR/" 2>/dev/null || true
    log "✓ Backup created: $BACKUP_DIR"
else
    warn "sites-enabled directory not found"
fi

# Step 2: Check for default_server conflicts
log "Step 2: Checking for default_server conflicts..."
DEFAULT_SERVER_FOUND=false

for config in /etc/nginx/sites-enabled/* /etc/nginx/sites-available/*; do
    if [ -f "$config" ] && grep -q "default_server" "$config" 2>/dev/null; then
        warn "Found default_server in: $config"
        DEFAULT_SERVER_FOUND=true
    fi
done

if [ "$DEFAULT_SERVER_FOUND" = true ]; then
    warn "⚠️  default_server found in existing configs!"
    warn "This may cause routing conflicts. Consider removing default_server from other configs."
    echo ""
    read -p "Continue anyway? (yes/no): " CONTINUE
    if [ "$CONTINUE" != "yes" ]; then
        info "Exiting. Please fix default_server conflicts first."
        exit 0
    fi
else
    log "✓ No default_server conflicts found"
fi

# Step 3: Copy nginx configs
log "Step 3: Installing nginx configurations..."

NGINX_CONFIG_DIR="/opt/mask-browser/infra/nginx"
if [ ! -d "$NGINX_CONFIG_DIR" ]; then
    error "Nginx config directory not found: $NGINX_CONFIG_DIR"
fi

# Copy configs to sites-available
for domain in wbmoneyback.ru maskbrowser.ru admin.maskbrowser.ru; do
    config_file="$NGINX_CONFIG_DIR/$domain.conf"
    if [ -f "$config_file" ]; then
        cp "$config_file" "/etc/nginx/sites-available/$domain.conf"
        log "✓ Copied $domain.conf"
    else
        warn "Config file not found: $config_file"
    fi
done

# Step 4: Create symlinks in sites-enabled with proper ordering
log "Step 4: Creating symlinks with proper ordering..."

# Remove old symlinks if they exist
rm -f /etc/nginx/sites-enabled/*maskbrowser*.conf
rm -f /etc/nginx/sites-enabled/*wbmoneyback*.conf

# Create symlinks with prefixes for proper ordering
# Order: 00-admin, 01-maskbrowser, 99-wbmoneyback (wbmoneyback last as fallback if needed)
ln -sf /etc/nginx/sites-available/admin.maskbrowser.ru.conf /etc/nginx/sites-enabled/00-admin.maskbrowser.ru.conf
ln -sf /etc/nginx/sites-available/maskbrowser.ru.conf /etc/nginx/sites-enabled/01-maskbrowser.ru.conf
ln -sf /etc/nginx/sites-available/wbmoneyback.ru.conf /etc/nginx/sites-enabled/99-wbmoneyback.ru.conf

log "✓ Symlinks created with proper ordering"

# Step 5: Verify nginx config syntax
log "Step 5: Verifying nginx configuration syntax..."
if nginx -t; then
    log "✓ Nginx configuration is valid"
else
    error "Nginx configuration test failed! Please check the errors above."
fi

# Step 6: Check SSL certificates
log "Step 6: Checking SSL certificates..."

check_cert() {
    local domain=$1
    if [ -f "/etc/letsencrypt/live/$domain/fullchain.pem" ]; then
        log "✓ Certificate exists for $domain"
        return 0
    else
        warn "✗ Certificate NOT found for $domain"
        return 1
    fi
}

CERT_ISSUES=false
check_cert "wbmoneyback.ru" || CERT_ISSUES=true
check_cert "maskbrowser.ru" || CERT_ISSUES=true
check_cert "admin.maskbrowser.ru" || CERT_ISSUES=true

if [ "$CERT_ISSUES" = true ]; then
    warn "Some certificates are missing!"
    echo ""
    info "To obtain certificates, run:"
    echo "  certbot certonly --webroot -w /var/www/certbot -d wbmoneyback.ru"
    echo "  certbot certonly --webroot -w /var/www/certbot -d maskbrowser.ru -d www.maskbrowser.ru"
    echo "  certbot certonly --webroot -w /var/www/certbot -d admin.maskbrowser.ru"
    echo ""
    read -p "Continue without all certificates? (yes/no): " CONTINUE_CERT
    if [ "$CONTINUE_CERT" != "yes" ]; then
        info "Exiting. Please obtain certificates first."
        exit 0
    fi
fi

# Step 7: Reload nginx
log "Step 7: Reloading nginx..."
if systemctl reload nginx; then
    log "✓ Nginx reloaded successfully"
else
    error "Failed to reload nginx!"
fi

# Step 8: Verify routing
log "Step 8: Verifying domain routing..."
echo ""

verify_domain() {
    local domain=$1
    info "Testing $domain..."
    
    # Test HTTP redirect
    HTTP_CODE=$(curl -sI "http://$domain" -o /dev/null -w "%{http_code}" --max-time 5 2>/dev/null || echo "000")
    if [ "$HTTP_CODE" = "301" ] || [ "$HTTP_CODE" = "302" ]; then
        log "  ✓ HTTP redirect works (code: $HTTP_CODE)"
    else
        warn "  ✗ HTTP redirect may not work (code: $HTTP_CODE)"
    fi
    
    # Test HTTPS (if certificate exists)
    if [ -f "/etc/letsencrypt/live/$domain/fullchain.pem" ]; then
        HTTPS_CODE=$(curl -sI "https://$domain" -o /dev/null -w "%{http_code}" --max-time 5 2>/dev/null || echo "000")
        if [ "$HTTPS_CODE" = "200" ] || [ "$HTTPS_CODE" = "503" ]; then
            log "  ✓ HTTPS works (code: $HTTPS_CODE)"
        else
            warn "  ✗ HTTPS may not work (code: $HTTPS_CODE)"
        fi
    else
        warn "  ⚠️  Skipping HTTPS test (no certificate)"
    fi
}

verify_domain "wbmoneyback.ru"
verify_domain "maskbrowser.ru"
verify_domain "admin.maskbrowser.ru"

# Step 9: Show routing summary
log "Step 9: Routing summary..."
echo ""
info "Domain routing configuration:"
echo ""
echo "  wbmoneyback.ru      → (configure your app here)"
echo "  maskbrowser.ru       → http://localhost:5052 (Client Web)"
echo "  admin.maskbrowser.ru → http://localhost:5100 (MaskAdmin)"
echo ""
info "Config files:"
echo "  /etc/nginx/sites-available/wbmoneyback.ru.conf"
echo "  /etc/nginx/sites-available/maskbrowser.ru.conf"
echo "  /etc/nginx/sites-available/admin.maskbrowser.ru.conf"
echo ""
info "Log files:"
echo "  /var/log/nginx/wbmoneyback.ru_access.log"
echo "  /var/log/nginx/maskbrowser.ru_access.log"
echo "  /var/log/nginx/admin.maskbrowser.ru_access.log"
echo ""
info "Backup location: $BACKUP_DIR"
echo ""

# Step 10: Test commands
log "Step 10: Useful test commands..."
echo ""
info "To test routing manually:"
echo "  curl -I -H 'Host: wbmoneyback.ru' http://109.172.101.73"
echo "  curl -I -H 'Host: maskbrowser.ru' http://109.172.101.73"
echo "  curl -I -H 'Host: admin.maskbrowser.ru' http://109.172.101.73"
echo ""
info "To check nginx config:"
echo "  nginx -t"
echo "  nginx -T | grep -A 5 'server_name'"
echo ""
info "To view logs:"
echo "  tail -f /var/log/nginx/*_access.log"
echo "  tail -f /var/log/nginx/*_error.log"
echo ""

log "=== Setup Complete ==="
log "All three domains are now configured in nginx!"
warn "⚠️  Don't forget to configure wbmoneyback.ru location block in the config file!"
