#!/bin/bash

# MaskAdmin Deployment Script
# Автоматически обновляет код из GitHub и перезапускает приложение

set -e  # Exit on error

# Configuration
REPO_DIR="/opt/mask-browser/MaskAdmin"
GIT_BRANCH="${GIT_BRANCH:-main}"
SERVICE_NAME="maskadmin"
BACKUP_DIR="/opt/mask-browser/backups"
LOG_FILE="/var/log/maskadmin-deploy.log"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Logging function
log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1" | tee -a "$LOG_FILE"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1" | tee -a "$LOG_FILE"
    exit 1
}

warn() {
    echo -e "${YELLOW}[WARNING]${NC} $1" | tee -a "$LOG_FILE"
}

# Check if running as root or with sudo
if [ "$EUID" -ne 0 ]; then
    error "Please run as root or with sudo"
fi

log "=== Starting MaskAdmin Deployment ==="

# Step 1: Create backup directory
log "Creating backup directory..."
mkdir -p "$BACKUP_DIR"

# Step 2: Backup current version
log "Creating backup of current version..."
BACKUP_NAME="maskadmin-$(date +%Y%m%d-%H%M%S).tar.gz"
cd "$REPO_DIR"
tar -czf "$BACKUP_DIR/$BACKUP_NAME" --exclude='bin' --exclude='obj' --exclude='node_modules' . || warn "Backup failed, continuing..."

# Keep only last 5 backups
log "Cleaning old backups (keeping last 5)..."
cd "$BACKUP_DIR"
ls -t maskadmin-*.tar.gz | tail -n +6 | xargs -r rm

# Step 3: Stop the service
log "Stopping $SERVICE_NAME service..."
systemctl stop "$SERVICE_NAME" || warn "Service may not be running"

# Step 4: Pull latest code from GitHub
log "Pulling latest code from GitHub (branch: $GIT_BRANCH)..."
cd "$REPO_DIR"

# Stash any local changes
git stash || true

# Fetch latest changes
git fetch origin "$GIT_BRANCH" || error "Failed to fetch from GitHub"

# Get current and new commit hashes
OLD_COMMIT=$(git rev-parse HEAD)
NEW_COMMIT=$(git rev-parse "origin/$GIT_BRANCH")

if [ "$OLD_COMMIT" = "$NEW_COMMIT" ]; then
    log "Already up to date (commit: $OLD_COMMIT)"
else
    log "Updating from $OLD_COMMIT to $NEW_COMMIT"

    # Show what changed
    log "Changes:"
    git log --oneline "$OLD_COMMIT..$NEW_COMMIT" | tee -a "$LOG_FILE"

    # Pull changes
    git pull origin "$GIT_BRANCH" || error "Failed to pull from GitHub"
fi

# Step 5: Restore dependencies
log "Restoring NuGet packages..."
dotnet restore || error "Failed to restore packages"

# Step 6: Apply database migrations
log "Applying database migrations..."
dotnet ef database update || error "Failed to apply migrations"

# Step 7: Build the project
log "Building project (Release)..."
dotnet build -c Release || error "Build failed"

# Step 8: Publish the project
log "Publishing project..."
dotnet publish -c Release -o "$REPO_DIR/publish" || error "Publish failed"

# Step 9: Set correct permissions
log "Setting permissions..."
chown -R www-data:www-data "$REPO_DIR/publish"
chmod -R 755 "$REPO_DIR/publish"

# Step 10: Start the service
log "Starting $SERVICE_NAME service..."
systemctl start "$SERVICE_NAME" || error "Failed to start service"

# Step 11: Wait for service to start
log "Waiting for service to start..."
sleep 5

# Step 12: Check service status
if systemctl is-active --quiet "$SERVICE_NAME"; then
    log "${GREEN}✓ Service is running${NC}"
else
    error "Service failed to start. Check logs: journalctl -u $SERVICE_NAME -n 50"
fi

# Step 13: Health check
log "Performing health check..."
if curl -f -s http://localhost:5000/health > /dev/null; then
    log "${GREEN}✓ Health check passed${NC}"
else
    warn "Health check failed, but service is running"
fi

# Step 14: Show recent logs
log "Recent service logs:"
journalctl -u "$SERVICE_NAME" -n 10 --no-pager | tee -a "$LOG_FILE"

log "=== Deployment completed successfully ==="
log "Backup saved to: $BACKUP_DIR/$BACKUP_NAME"
log "Deployed commit: $NEW_COMMIT"

# Optional: Clean up old published files
log "Cleaning up..."
find "$REPO_DIR/bin" -type d -name "Debug" -o -name "Release" | xargs -r rm -rf
find "$REPO_DIR/obj" -type d | xargs -r rm -rf

log "Done!"
