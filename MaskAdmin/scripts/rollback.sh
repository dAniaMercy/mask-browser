#!/bin/bash

# MaskAdmin Rollback Script
# Откатывает приложение к предыдущей версии из бэкапа

set -e

# Configuration
REPO_DIR="/opt/mask-browser/MaskAdmin"
SERVICE_NAME="maskadmin"
BACKUP_DIR="/opt/mask-browser/backups"
LOG_FILE="/var/log/maskadmin-deploy.log"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1" | tee -a "$LOG_FILE"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1" | tee -a "$LOG_FILE"
    exit 1
}

if [ "$EUID" -ne 0 ]; then
    error "Please run as root or with sudo"
fi

log "=== Starting MaskAdmin Rollback ==="

# List available backups
cd "$BACKUP_DIR"
BACKUPS=($(ls -t maskadmin-*.tar.gz 2>/dev/null))

if [ ${#BACKUPS[@]} -eq 0 ]; then
    error "No backups found in $BACKUP_DIR"
fi

echo ""
echo "Available backups:"
for i in "${!BACKUPS[@]}"; do
    echo "  $((i+1))) ${BACKUPS[$i]}"
done
echo ""

# Select backup
if [ -n "$1" ]; then
    BACKUP_INDEX=$((${1} - 1))
else
    read -p "Select backup number (default: 1 = latest): " BACKUP_NUM
    BACKUP_NUM=${BACKUP_NUM:-1}
    BACKUP_INDEX=$((BACKUP_NUM - 1))
fi

if [ $BACKUP_INDEX -lt 0 ] || [ $BACKUP_INDEX -ge ${#BACKUPS[@]} ]; then
    error "Invalid backup number"
fi

BACKUP_FILE="${BACKUPS[$BACKUP_INDEX]}"
log "Selected backup: $BACKUP_FILE"

# Confirm rollback
read -p "Are you sure you want to rollback to $BACKUP_FILE? (yes/no): " CONFIRM
if [ "$CONFIRM" != "yes" ]; then
    log "Rollback cancelled"
    exit 0
fi

# Stop service
log "Stopping $SERVICE_NAME service..."
systemctl stop "$SERVICE_NAME" || true

# Backup current state before rollback
log "Creating backup of current state before rollback..."
CURRENT_BACKUP="maskadmin-before-rollback-$(date +%Y%m%d-%H%M%S).tar.gz"
cd "$REPO_DIR"
tar -czf "$BACKUP_DIR/$CURRENT_BACKUP" --exclude='bin' --exclude='obj' . || true

# Extract backup
log "Extracting backup..."
cd "$REPO_DIR"
tar -xzf "$BACKUP_DIR/$BACKUP_FILE" || error "Failed to extract backup"

# Restore dependencies
log "Restoring dependencies..."
dotnet restore || error "Failed to restore packages"

# Rebuild
log "Rebuilding project..."
dotnet build -c Release || error "Build failed"

# Republish
log "Publishing project..."
dotnet publish -c Release -o "$REPO_DIR/publish" || error "Publish failed"

# Set permissions
log "Setting permissions..."
chown -R www-data:www-data "$REPO_DIR/publish"
chmod -R 755 "$REPO_DIR/publish"

# Start service
log "Starting $SERVICE_NAME service..."
systemctl start "$SERVICE_NAME" || error "Failed to start service"

# Wait and check
log "Waiting for service to start..."
sleep 5

if systemctl is-active --quiet "$SERVICE_NAME"; then
    log "${GREEN}✓ Service is running${NC}"
else
    error "Service failed to start. Check logs: journalctl -u $SERVICE_NAME -n 50"
fi

# Health check
log "Performing health check..."
if curl -f -s http://localhost:5000/health > /dev/null; then
    log "${GREEN}✓ Health check passed${NC}"
else
    log "${YELLOW}⚠ Health check failed, but service is running${NC}"
fi

log "=== Rollback completed successfully ==="
log "Rolled back to: $BACKUP_FILE"
log "Current state backed up to: $CURRENT_BACKUP"
log ""
log "Recent service logs:"
journalctl -u "$SERVICE_NAME" -n 10 --no-pager
