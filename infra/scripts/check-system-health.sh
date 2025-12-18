#!/bin/bash

# Скрипт для проверки состояния системы MaskBrowser
# Проверяет контейнеры, API, базу данных и другие компоненты

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[✓]${NC} $1"
}

warn() {
    echo -e "${YELLOW}[⚠]${NC} $1"
}

error() {
    echo -e "${RED}[✗]${NC} $1"
}

info() {
    echo -e "${BLUE}[i]${NC} $1"
}

echo "=== MaskBrowser System Health Check ==="
echo ""

# 1. Проверка Docker
info "1. Checking Docker..."
if command -v docker &> /dev/null; then
    if docker ps &> /dev/null; then
        log "Docker is running"
        CONTAINER_COUNT=$(docker ps -q | wc -l)
        info "  Active containers: $CONTAINER_COUNT"
    else
        error "Docker is not accessible (permissions?)"
    fi
else
    error "Docker is not installed"
fi
echo ""

# 2. Проверка контейнеров MaskBrowser
info "2. Checking MaskBrowser containers..."
MASKBROWSER_CONTAINERS=$(docker ps --filter "name=maskbrowser" --format "{{.Names}}" 2>/dev/null || echo "")

if [ -z "$MASKBROWSER_CONTAINERS" ]; then
    warn "No MaskBrowser containers found"
else
    log "Found MaskBrowser containers:"
    echo "$MASKBROWSER_CONTAINERS" | while read container; do
        STATUS=$(docker inspect --format='{{.State.Status}}' "$container" 2>/dev/null || echo "unknown")
        if [ "$STATUS" = "running" ]; then
            echo "  ✓ $container (running)"
        else
            echo "  ✗ $container ($STATUS)"
        fi
    done
fi
echo ""

# 3. Проверка API
info "3. Checking API server..."
API_URL="${API_URL:-http://localhost:5050}"
if curl -s -f "$API_URL/health" &> /dev/null; then
    log "API server is responding"
    HEALTH_RESPONSE=$(curl -s "$API_URL/health" 2>/dev/null || echo "{}")
    echo "  Response: $HEALTH_RESPONSE"
else
    error "API server is not responding at $API_URL"
fi
echo ""

# 4. Проверка базы данных
info "4. Checking database connection..."
if docker exec maskbrowser-postgres pg_isready -U maskuser &> /dev/null 2>&1; then
    log "PostgreSQL is accessible"
    DB_CONNECTIONS=$(docker exec maskbrowser-postgres psql -U maskuser -d maskbrowser -t -c "SELECT count(*) FROM pg_stat_activity;" 2>/dev/null || echo "0")
    info "  Active connections: $DB_CONNECTIONS"
else
    error "PostgreSQL is not accessible"
fi
echo ""

# 5. Проверка Redis
info "5. Checking Redis..."
if docker exec maskbrowser-redis redis-cli ping &> /dev/null 2>&1; then
    log "Redis is accessible"
else
    error "Redis is not accessible"
fi
echo ""

# 6. Проверка профилей браузера
info "6. Checking browser profiles..."
if [ -n "$API_URL" ] && curl -s -f "$API_URL/health" &> /dev/null; then
    # Если есть токен авторизации, можно проверить профили
    if [ -n "$API_TOKEN" ]; then
        PROFILES_RESPONSE=$(curl -s -H "Authorization: Bearer $API_TOKEN" "$API_URL/api/profile" 2>/dev/null || echo "[]")
        PROFILE_COUNT=$(echo "$PROFILES_RESPONSE" | grep -o '"id"' | wc -l || echo "0")
        info "  Total profiles (via API): $PROFILE_COUNT"
    else
        info "  Set API_TOKEN to check profiles via API"
    fi
else
    warn "Cannot check profiles (API not available)"
fi
echo ""

# 7. Проверка дискового пространства
info "7. Checking disk space..."
DISK_USAGE=$(df -h / | awk 'NR==2 {print $5}' | sed 's/%//')
if [ "$DISK_USAGE" -lt 80 ]; then
    log "Disk usage: ${DISK_USAGE}%"
elif [ "$DISK_USAGE" -lt 90 ]; then
    warn "Disk usage: ${DISK_USAGE}% (getting high)"
else
    error "Disk usage: ${DISK_USAGE}% (critical!)"
fi
echo ""

# 8. Проверка памяти
info "8. Checking memory..."
MEMORY_USAGE=$(free | awk 'NR==2{printf "%.0f", $3*100/$2}')
if [ "$MEMORY_USAGE" -lt 80 ]; then
    log "Memory usage: ${MEMORY_USAGE}%"
elif [ "$MEMORY_USAGE" -lt 90 ]; then
    warn "Memory usage: ${MEMORY_USAGE}% (getting high)"
else
    error "Memory usage: ${MEMORY_USAGE}% (critical!)"
fi
echo ""

# 9. Проверка логов на ошибки
info "9. Checking recent errors in logs..."
ERROR_COUNT=$(docker logs maskbrowser-api --since 5m 2>&1 | grep -i "error\|exception\|failed" | wc -l || echo "0")
if [ "$ERROR_COUNT" -eq 0 ]; then
    log "No recent errors in API logs"
else
    warn "Found $ERROR_COUNT error(s) in last 5 minutes"
    info "  Run 'docker logs maskbrowser-api --tail 50' to see details"
fi
echo ""

# 10. Проверка метрик Prometheus (если доступен)
info "10. Checking Prometheus metrics..."
PROMETHEUS_URL="${PROMETHEUS_URL:-http://localhost:9090}"
if curl -s -f "$PROMETHEUS_URL/api/v1/status/config" &> /dev/null; then
    log "Prometheus is accessible"
    ACTIVE_CONTAINERS=$(curl -s "$PROMETHEUS_URL/api/v1/query?query=maskbrowser_containers_active" 2>/dev/null | grep -o '"value":\[.*\]' | cut -d',' -f2 | tr -d ']' || echo "N/A")
    info "  Active containers (from metrics): $ACTIVE_CONTAINERS"
else
    warn "Prometheus is not accessible at $PROMETHEUS_URL"
fi
echo ""

# Итоговый статус
echo "=== Health Check Summary ==="
echo ""
log "Health check completed"
echo ""
info "For detailed diagnostics, use:"
echo "  - API diagnostics: curl -H 'Authorization: Bearer TOKEN' $API_URL/api/diagnostics/containers"
echo "  - Container logs: docker logs maskbrowser-api --tail 100"
echo "  - System metrics: curl $PROMETHEUS_URL/api/v1/query?query=maskbrowser_containers_active"
echo ""
