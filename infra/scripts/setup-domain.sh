#!/bin/bash

# MaskBrowser Domain Setup Script
# Настройка доменов maskbrowser.ru и admin.maskbrowser.ru
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

log "=== MaskBrowser Domain Setup ==="
echo ""

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
log "Step 2: Creating certbot webroot..."
mkdir -p /var/www/certbot
chown -R www-data:www-data /var/www/certbot

# Step 3: Copy Nginx configurations
log "Step 3: Installing Nginx configurations..."

# Copy configs
cp /opt/mask-browser/infra/nginx/maskbrowser.ru.conf /etc/nginx/sites-available/
cp /opt/mask-browser/infra/nginx/admin.maskbrowser.ru.conf /etc/nginx/sites-available/

# Create symlinks
ln -sf /etc/nginx/sites-available/maskbrowser.ru.conf /etc/nginx/sites-enabled/
ln -sf /etc/nginx/sites-available/admin.maskbrowser.ru.conf /etc/nginx/sites-enabled/

log "Nginx configurations installed"

# Step 4: Test Nginx configuration
log "Step 4: Testing Nginx configuration..."
nginx -t || error "Nginx configuration test failed"

# Step 5: Reload Nginx (without SSL first)
log "Step 5: Reloading Nginx..."

# Temporarily disable SSL sections for initial setup
sed -i '/listen 443/,/^}/s/^/#/' /etc/nginx/sites-available/maskbrowser.ru.conf
sed -i '/listen 443/,/^}/s/^/#/' /etc/nginx/sites-available/admin.maskbrowser.ru.conf

nginx -t && systemctl reload nginx || error "Failed to reload Nginx"

# Step 6: Check DNS before obtaining certificates
log "Step 6: Checking DNS records..."
echo ""
info "Please ensure your DNS records are configured:"
echo ""
echo "  Domain: maskbrowser.ru"
echo "  Type: A"
echo "  Value: 109.172.101.73"
echo ""
echo "  Domain: www.maskbrowser.ru"
echo "  Type: A"
echo "  Value: 109.172.101.73"
echo ""
echo "  Domain: admin.maskbrowser.ru"
echo "  Type: A"
echo "  Value: 109.172.101.73"
echo ""

# Check DNS resolution
info "Testing DNS resolution..."
for domain in maskbrowser.ru www.maskbrowser.ru admin.maskbrowser.ru; do
    if host $domain | grep -q "109.172.101.73"; then
        log "✓ $domain resolves correctly"
    else
        warn "✗ $domain does NOT resolve to 109.172.101.73"
        warn "  Current resolution: $(host $domain | grep 'has address' || echo 'Not found')"
    fi
done
echo ""

read -p "Do DNS records resolve correctly? Continue with SSL setup? (yes/no): " CONTINUE
if [ "$CONTINUE" != "yes" ]; then
    warn "Exiting. Please configure DNS and run this script again."
    exit 0
fi

# Step 7: Obtain SSL certificates
log "Step 7: Obtaining SSL certificates with Let's Encrypt..."

# Certificate for main domain (maskbrowser.ru + www.maskbrowser.ru)
log "Obtaining certificate for maskbrowser.ru..."
certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    --force-renewal \
    -d maskbrowser.ru \
    -d www.maskbrowser.ru || error "Failed to obtain certificate for maskbrowser.ru"

log "✓ Certificate obtained for maskbrowser.ru"

# Certificate for admin subdomain
log "Obtaining certificate for admin.maskbrowser.ru..."
certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    --force-renewal \
    -d admin.maskbrowser.ru || error "Failed to obtain certificate for admin.maskbrowser.ru"

log "✓ Certificate obtained for admin.maskbrowser.ru"

# Step 8: Enable SSL in Nginx configs
log "Step 8: Enabling SSL in Nginx configurations..."

# Restore SSL sections
sed -i '/listen 443/,/^}/s/^#//' /etc/nginx/sites-available/maskbrowser.ru.conf
sed -i '/listen 443/,/^}/s/^#//' /etc/nginx/sites-available/admin.maskbrowser.ru.conf

# Test configuration
nginx -t || error "Nginx configuration test failed after enabling SSL"

# Reload Nginx
systemctl reload nginx || error "Failed to reload Nginx"

log "✓ SSL enabled and Nginx reloaded"

# Step 9: Setup automatic certificate renewal
log "Step 9: Setting up automatic certificate renewal..."

# Create renewal hook
cat > /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh <<'EOF'
#!/bin/bash
systemctl reload nginx
EOF

chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh

# Test renewal (dry run)
log "Testing certificate renewal (dry run)..."
certbot renew --dry-run || warn "Certificate renewal test failed (not critical for now)"

log "✓ Automatic renewal configured"

# Step 10: Configure firewall
log "Step 10: Configuring firewall..."

if command -v ufw &> /dev/null; then
    ufw allow 80/tcp comment 'HTTP'
    ufw allow 443/tcp comment 'HTTPS'
    ufw status | grep -E '80|443'
    log "✓ Firewall configured"
else
    warn "UFW not installed, skipping firewall configuration"
fi

# Step 11: Verify setup
log "Step 11: Verifying setup..."
echo ""

# Check if services are accessible
info "Checking services..."

# Check main site
if curl -sI https://maskbrowser.ru | grep -q "HTTP.*200\|HTTP.*301\|HTTP.*302"; then
    log "✓ maskbrowser.ru is accessible"
else
    warn "✗ maskbrowser.ru is NOT accessible"
fi

# Check admin site
if curl -sI https://admin.maskbrowser.ru | grep -q "HTTP.*200\|HTTP.*301\|HTTP.*302"; then
    log "✓ admin.maskbrowser.ru is accessible"
else
    warn "✗ admin.maskbrowser.ru is NOT accessible"
fi

# Check SSL
info "Checking SSL certificates..."
echo | openssl s_client -connect maskbrowser.ru:443 -servername maskbrowser.ru 2>/dev/null | grep -q "Verify return code: 0" && log "✓ maskbrowser.ru SSL valid" || warn "✗ maskbrowser.ru SSL may have issues"
echo | openssl s_client -connect admin.maskbrowser.ru:443 -servername admin.maskbrowser.ru 2>/dev/null | grep -q "Verify return code: 0" && log "✓ admin.maskbrowser.ru SSL valid" || warn "✗ admin.maskbrowser.ru SSL may have issues"

echo ""
log "=== Setup Complete ==="
echo ""
info "Your sites are now available at:"
echo "  - https://maskbrowser.ru (Client Web)"
echo "  - https://admin.maskbrowser.ru (Admin Panel)"
echo ""
info "SSL certificates will auto-renew via certbot"
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
