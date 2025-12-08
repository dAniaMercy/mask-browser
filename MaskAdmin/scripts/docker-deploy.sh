#!/bin/bash

# MaskAdmin Docker Deployment Script
# Обновляет и перезапускает MaskAdmin через Docker Compose

set -e

# Configuration
INFRA_DIR="/opt/mask-browser/infra"
MASKADMIN_DIR="/opt/mask-browser/MaskAdmin"
GIT_BRANCH="${GIT_BRANCH:-main}"
BACKUP_DIR="/opt/mask-browser/backups"
LOG_FILE="/var/log/maskadmin-docker-deploy.log"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
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

log "=== Starting MaskAdmin Docker Deployment ==="

# Step 1: Backup current configuration
log "Creating backup..."
mkdir -p "$BACKUP_DIR"
BACKUP_NAME="maskadmin-docker-$(date +%Y%m%d-%H%M%S).tar.gz"
cd "$MASKADMIN_DIR"
tar -czf "$BACKUP_DIR/$BACKUP_NAME" --exclude='bin' --exclude='obj' . || true

# Step 2: Pull latest code
log "Pulling latest code from GitHub (branch: $GIT_BRANCH)..."
cd "$MASKADMIN_DIR"
git stash || true
git fetch origin "$GIT_BRANCH" || error "Failed to fetch from GitHub"

OLD_COMMIT=$(git rev-parse HEAD)
NEW_COMMIT=$(git rev-parse "origin/$GIT_BRANCH")

if [ "$OLD_COMMIT" = "$NEW_COMMIT" ]; then
    log "Already up to date (commit: $OLD_COMMIT)"
else
    log "Updating from $OLD_COMMIT to $NEW_COMMIT"
    git log --oneline "$OLD_COMMIT..$NEW_COMMIT" | tee -a "$LOG_FILE"
    git pull origin "$GIT_BRANCH" || error "Failed to pull from GitHub"
fi

# Step 3: Stop current container
log "Stopping MaskAdmin container..."
cd "$INFRA_DIR"
docker-compose stop maskadmin || true

# Step 4: Rebuild image
log "Building new Docker image..."
docker-compose build --no-cache maskadmin || error "Docker build failed"

# Step 5: Apply migrations
log "Applying database migrations..."
docker-compose run --rm maskadmin dotnet ef database update || error "Migration failed"

# Step 6: Start container
log "Starting MaskAdmin container..."
docker-compose up -d maskadmin || error "Failed to start container"

# Step 7: Wait for container to be healthy
log "Waiting for container to be healthy..."
for i in {1..30}; do
    if docker-compose ps maskadmin | grep -q "Up"; then
        log "${GREEN}✓ Container is up${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        error "Container failed to start"
    fi
    sleep 2
done

# Step 8: Check logs
log "Recent container logs:"
docker-compose logs --tail=20 maskadmin | tee -a "$LOG_FILE"

# Step 9: Health check
sleep 5
log "Performing health check..."
CONTAINER_IP=$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $(docker-compose ps -q maskadmin))
if curl -f -s "http://$CONTAINER_IP:5000/health" > /dev/null 2>&1; then
    log "${GREEN}✓ Health check passed${NC}"
else
    log "${YELLOW}⚠ Health check failed (this may be normal if health endpoint is not configured)${NC}"
fi

# Step 10: Clean up old images
log "Cleaning up old Docker images..."
docker image prune -f || true

log "=== Docker deployment completed successfully ==="
log "Backup saved to: $BACKUP_DIR/$BACKUP_NAME"
log "Deployed commit: $NEW_COMMIT"
log ""
log "To view logs: docker-compose -f $INFRA_DIR/docker-compose.yml logs -f maskadmin"
log "To check status: docker-compose -f $INFRA_DIR/docker-compose.yml ps maskadmin"
