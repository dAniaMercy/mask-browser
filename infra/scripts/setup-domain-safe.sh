#!/bin/bash

# MaskBrowser Domain Setup Script (Safe for multi-site servers)
# Безопасная настройка для серверов с несколькими сайтами
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

log "=== MaskBrowser Domain Setup (Multi-Site Safe) ==="
echo ""

# Step 0: Check existing sites
log "Step 0: Checking existing Nginx configuration..."

info "Existing Nginx sites:"
ls -1 /etc/nginx/sites-enabled/ | while read site; do
    echo "  - $site"
done
echo ""

info "Checking for default_server directives..."
if grep -r "default_server" /etc/nginx/sites-enabled/ 2>/dev/null; then
    warn "Found default_server in existing configs"
    warn "MaskBrowser configs will NOT use default_server to avoid conflicts"
else
    info "No default_server found"
fi
echo ""

info "Existing domains:"
grep -rh "server_name" /etc/nginx/sites-enabled/ 2>/dev/null | grep -v "#" | sort -u || echo "  None found"
echo ""

read -p "Continue with MaskBrowser setup? (yes/no): " CONTINUE
if [ "$CONTINUE" != "yes" ]; then
    info "Exiting."
    exit 0
fi

# Step 1: Install certbot if not installed
log "Step 1: Checking certbot installation..."
if ! command -v certbot &> /dev/null; then
    log "Installing certbot..."
    apt update
    apt install -y certbot python3-certbot-nginx
else
    info "Certbot already installed: $(certbot --version)"
fi

# Step 2: Create certbot webroot
log "Step 2: Ensuring certbot webroot exists..."
mkdir -p /var/www/certbot
chown -R www-data:www-data /var/www/certbot

# Step 3: Backup existing configs (if any)
log "Step 3: Creating backup of existing configs..."
BACKUP_DIR="/root/nginx-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"
cp -r /etc/nginx/sites-enabled/* "$BACKUP_DIR/" 2>/dev/null || true
log "Backup created: $BACKUP_DIR"

# Step 4: Copy MaskBrowser Nginx configurations
log "Step 4: Installing MaskBrowser Nginx configurations..."

# Check if configs already exist
if [ -f /etc/nginx/sites-enabled/maskbrowser.ru.conf ]; then
    warn "maskbrowser.ru.conf already exists, backing up..."
    mv /etc/nginx/sites-enabled/maskbrowser.ru.conf "$BACKUP_DIR/"
fi

if [ -f /etc/nginx/sites-enabled/admin.maskbrowser.ru.conf ]; then
    warn "admin.maskbrowser.ru.conf already exists, backing up..."
    mv /etc/nginx/sites-enabled/admin.maskbrowser.ru.conf "$BACKUP_DIR/"
fi

# Copy new configs
cp /opt/mask-browser/infra/nginx/maskbrowser.ru.conf /etc/nginx/sites-available/
cp /opt/mask-browser/infra/nginx/admin.maskbrowser.ru.conf /etc/nginx/sites-available/

# Create symlinks
ln -sf /etc/nginx/sites-available/maskbrowser.ru.conf /etc/nginx/sites-enabled/
ln -sf /etc/nginx/sites-available/admin.maskbrowser.ru.conf /etc/nginx/sites-enabled/

log "MaskBrowser configs installed"

# Step 5: Temporarily disable SSL sections
log "Step 5: Temporarily disabling SSL sections for certificate acquisition..."

# Create temporary configs without SSL
cp /etc/nginx/sites-available/maskbrowser.ru.conf /tmp/maskbrowser.ru.conf.backup
cp /etc/nginx/sites-available/admin.maskbrowser.ru.conf /tmp/admin.maskbrowser.ru.conf.backup

# Comment out SSL blocks
sed -i '/listen 443/,/^}/s/^/#/' /etc/nginx/sites-available/maskbrowser.ru.conf
sed -i '/listen 443/,/^}/s/^/#/' /etc/nginx/sites-available/admin.maskbrowser.ru.conf

# Step 6: Test Nginx configuration
log "Step 6: Testing Nginx configuration..."
if nginx -t; then
    log "✓ Nginx configuration is valid"
else
    error "Nginx configuration test failed. Restoring backups..."
    cp /tmp/maskbrowser.ru.conf.backup /etc/nginx/sites-available/maskbrowser.ru.conf
    cp /tmp/admin.maskbrowser.ru.conf.backup /etc/nginx/sites-available/admin.maskbrowser.ru.conf
    exit 1
fi

# Step 7: Reload Nginx
log "Step 7: Reloading Nginx..."
systemctl reload nginx || error "Failed to reload Nginx"

# Step 8: Check DNS
log "Step 8: Checking DNS records..."
echo ""
info "Verifying DNS resolution..."

DNS_OK=true
for domain in maskbrowser.ru www.maskbrowser.ru admin.maskbrowser.ru; do
    if host $domain | grep -q "109.172.101.73"; then
        log "✓ $domain resolves correctly"
    else
        warn "✗ $domain does NOT resolve to 109.172.101.73"
        warn "  Current: $(host $domain | grep 'has address' || echo 'Not found')"
        DNS_OK=false
    fi
done
echo ""

if [ "$DNS_OK" = false ]; then
    warn "DNS records are not fully configured!"
    echo ""
    info "Please configure DNS records at your registrar:"
    echo ""
    echo "  Type: A    Name: @      Value: 109.172.101.73"
    echo "  Type: A    Name: www    Value: 109.172.101.73"
    echo "  Type: A    Name: admin  Value: 109.172.101.73"
    echo ""
    read -p "Continue anyway? (yes/no): " CONTINUE_DNS
    if [ "$CONTINUE_DNS" != "yes" ]; then
        warn "Exiting. Configure DNS and run this script again."
        # Restore configs
        cp /tmp/maskbrowser.ru.conf.backup /etc/nginx/sites-available/maskbrowser.ru.conf
        cp /tmp/admin.maskbrowser.ru.conf.backup /etc/nginx/sites-available/admin.maskbrowser.ru.conf
        systemctl reload nginx
        exit 0
    fi
fi

# Step 9: Obtain SSL certificates
log "Step 9: Obtaining SSL certificates with Let's Encrypt..."

# Check if certificates already exist
if [ -d "/etc/letsencrypt/live/maskbrowser.ru" ]; then
    warn "Certificate for maskbrowser.ru already exists"
    read -p "Renew certificate? (yes/no): " RENEW_MAIN
    if [ "$RENEW_MAIN" = "yes" ]; then
        certbot certonly \
            --webroot \
            --webroot-path=/var/www/certbot \
            --email admin@maskbrowser.ru \
            --agree-tos \
            --no-eff-email \
            --force-renewal \
            -d maskbrowser.ru \
            -d www.maskbrowser.ru || warn "Failed to renew certificate for maskbrowser.ru"
    fi
else
    log "Obtaining certificate for maskbrowser.ru..."
    certbot certonly \
        --webroot \
        --webroot-path=/var/www/certbot \
        --email admin@maskbrowser.ru \
        --agree-tos \
        --no-eff-email \
        -d maskbrowser.ru \
        -d www.maskbrowser.ru || error "Failed to obtain certificate for maskbrowser.ru"
fi

log "✓ Certificate for maskbrowser.ru ready"

# Certificate for admin subdomain
if [ -d "/etc/letsencrypt/live/admin.maskbrowser.ru" ]; then
    warn "Certificate for admin.maskbrowser.ru already exists"
    read -p "Renew certificate? (yes/no): " RENEW_ADMIN
    if [ "$RENEW_ADMIN" = "yes" ]; then
        certbot certonly \
            --webroot \
            --webroot-path=/var/www/certbot \
            --email admin@maskbrowser.ru \
            --agree-tos \
            --no-eff-email \
            --force-renewal \
            -d admin.maskbrowser.ru || warn "Failed to renew certificate for admin.maskbrowser.ru"
    fi
else
    log "Obtaining certificate for admin.maskbrowser.ru..."
    certbot certonly \
        --webroot \
        --webroot-path=/var/www/certbot \
        --email admin@maskbrowser.ru \
        --agree-tos \
        --no-eff-email \
        -d admin.maskbrowser.ru || error "Failed to obtain certificate for admin.maskbrowser.ru"
fi

log "✓ Certificate for admin.maskbrowser.ru ready"

# Step 10: Enable SSL in Nginx configs
log "Step 10: Enabling SSL in Nginx configurations..."

# Restore full configs with SSL
cp /tmp/maskbrowser.ru.conf.backup /etc/nginx/sites-available/maskbrowser.ru.conf
cp /tmp/admin.maskbrowser.ru.conf.backup /etc/nginx/sites-available/admin.maskbrowser.ru.conf

# Test configuration
if nginx -t; then
    log "✓ Nginx configuration with SSL is valid"
else
    error "Nginx configuration test failed with SSL enabled"
fi

# Reload Nginx
systemctl reload nginx || error "Failed to reload Nginx with SSL"

log "✓ SSL enabled and Nginx reloaded"

# Step 11: Setup automatic certificate renewal (if not already)
log "Step 11: Ensuring automatic certificate renewal..."

if [ ! -f /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh ]; then
    cat > /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh <<'EOF'
#!/bin/bash
systemctl reload nginx
EOF
    chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh
    log "✓ Renewal hook created"
else
    info "Renewal hook already exists"
fi

# Test renewal (dry run)
log "Testing certificate renewal (dry run)..."
certbot renew --dry-run || warn "Certificate renewal test failed (not critical)"

# Step 12: Configure firewall (if needed)
log "Step 12: Checking firewall..."

if command -v ufw &> /dev/null; then
    if ufw status | grep -q "Status: active"; then
        info "UFW is active, ensuring ports are open..."
        ufw allow 80/tcp comment 'HTTP' 2>/dev/null || true
        ufw allow 443/tcp comment 'HTTPS' 2>/dev/null || true
        log "✓ Firewall configured"
    else
        info "UFW is installed but not active"
    fi
else
    info "UFW not installed, skipping firewall configuration"
fi

# Step 13: Verify setup
log "Step 13: Verifying setup..."
echo ""

# Check if services are accessible
info "Checking services..."

sleep 2  # Give Nginx a moment to fully reload

# Check main site
if curl -sI https://maskbrowser.ru --max-time 5 2>/dev/null | head -n 1 | grep -qE "HTTP.*[23][0-9]{2}"; then
    log "✓ maskbrowser.ru is accessible"
else
    warn "✗ maskbrowser.ru may not be accessible yet (check logs)"
fi

# Check admin site
if curl -sI https://admin.maskbrowser.ru --max-time 5 2>/dev/null | head -n 1 | grep -qE "HTTP.*[23][0-9]{2}"; then
    log "✓ admin.maskbrowser.ru is accessible"
else
    warn "✗ admin.maskbrowser.ru may not be accessible yet (check logs)"
fi

# Check SSL validity
info "Checking SSL certificates..."
if echo | openssl s_client -connect maskbrowser.ru:443 -servername maskbrowser.ru 2>/dev/null | grep -q "Verify return code: 0"; then
    log "✓ maskbrowser.ru SSL valid"
else
    warn "✗ maskbrowser.ru SSL may have issues"
fi

if echo | openssl s_client -connect admin.maskbrowser.ru:443 -servername admin.maskbrowser.ru 2>/dev/null | grep -q "Verify return code: 0"; then
    log "✓ admin.maskbrowser.ru SSL valid"
else
    warn "✗ admin.maskbrowser.ru SSL may have issues"
fi

# Clean up temporary files
rm -f /tmp/maskbrowser.ru.conf.backup /tmp/admin.maskbrowser.ru.conf.backup

echo ""
log "=== Setup Complete ==="
echo ""
info "Your MaskBrowser sites are now available at:"
echo "  - https://maskbrowser.ru (Client Web)"
echo "  - https://admin.maskbrowser.ru (Admin Panel)"
echo ""
info "Existing sites should continue working normally."
echo ""
info "Backup of previous configs saved to: $BACKUP_DIR"
echo ""
info "Logs:"
echo "  - Nginx access: /var/log/nginx/maskbrowser.ru_access.log"
echo "  - Nginx errors: /var/log/nginx/maskbrowser.ru_error.log"
echo "  - Admin access: /var/log/nginx/admin.maskbrowser.ru_access.log"
echo "  - Admin errors: /var/log/nginx/admin.maskbrowser.ru_error.log"
echo ""
info "To test:"
echo "  curl -I https://maskbrowser.ru"
echo "  curl -I https://admin.maskbrowser.ru"
echo ""
log "Done!"
