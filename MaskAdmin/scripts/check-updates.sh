#!/bin/bash

# MaskAdmin Update Checker
# Проверяет наличие обновлений на GitHub без применения изменений

set -e

# Configuration
REPO_DIR="/opt/mask-browser/MaskAdmin"
GIT_BRANCH="${GIT_BRANCH:-main}"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}=== Checking for updates ===${NC}"

cd "$REPO_DIR"

# Fetch latest changes
echo "Fetching latest changes from GitHub..."
git fetch origin "$GIT_BRANCH" 2>/dev/null || {
    echo -e "${YELLOW}Warning: Failed to fetch from GitHub${NC}"
    exit 1
}

# Get current and latest commit
CURRENT_COMMIT=$(git rev-parse HEAD)
LATEST_COMMIT=$(git rev-parse "origin/$GIT_BRANCH")

echo ""
echo "Current commit: $CURRENT_COMMIT"
echo "Latest commit:  $LATEST_COMMIT"
echo ""

if [ "$CURRENT_COMMIT" = "$LATEST_COMMIT" ]; then
    echo -e "${GREEN}✓ Already up to date${NC}"
    exit 0
fi

# Count commits behind
COMMITS_BEHIND=$(git rev-list --count HEAD..origin/$GIT_BRANCH)
echo -e "${YELLOW}⚠ $COMMITS_BEHIND commit(s) behind${NC}"
echo ""

# Show changes
echo "Recent changes:"
git log --oneline --graph --decorate HEAD..origin/$GIT_BRANCH

echo ""
echo "Files changed:"
git diff --stat HEAD..origin/$GIT_BRANCH

echo ""
echo -e "${BLUE}To update, run: sudo ./deploy.sh${NC}"
