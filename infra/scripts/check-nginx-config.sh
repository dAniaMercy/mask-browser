#!/bin/bash

# Скрипт для проверки конфигурации nginx и диагностики проблем с ACME challenge

set -e

# Цвета
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

info() {
    echo -e "${BLUE}[CHECK]${NC} $1"
}

if [ "$EUID" -ne 0 ]; then
    error "Пожалуйста, запустите скрипт с правами root или через sudo"
    exit 1
fi

echo "=== Диагностика конфигурации nginx ==="
echo ""

# 1. Проверка синтаксиса nginx
info "1. Проверка синтаксиса nginx..."
if nginx -t 2>&1 | grep -q "successful"; then
    log "   ✓ Синтаксис nginx корректен"
else
    error "   ✗ Ошибка в синтаксисе nginx:"
    nginx -t
    exit 1
fi

echo ""

# 2. Проверка конфигов для maskbrowser.ru
info "2. Проверка конфигурации maskbrowser.ru..."

CONFIG_FILE="/etc/nginx/sites-available/maskbrowser.ru.conf"

if [ ! -f "$CONFIG_FILE" ]; then
    error "   ✗ Файл $CONFIG_FILE не найден"
else
    log "   ✓ Файл найден: $CONFIG_FILE"
    
    # Проверка HTTP блока (порт 80)
    echo ""
    info "   HTTP блок (порт 80):"
    if grep -q "listen 80" "$CONFIG_FILE"; then
        log "   ✓ Порт 80 настроен"
        
        # Показать блок server для порта 80
        echo "   Содержимое HTTP блока:"
        sed -n '/server {/,/^}/p' "$CONFIG_FILE" | head -20 | sed 's/^/   /'
        
        # Проверка ACME challenge
        if grep -q "\.well-known/acme-challenge" "$CONFIG_FILE"; then
            log "   ✓ Секция ACME challenge найдена"
            
            # Проверить порядок - должна быть ПЕРЕД location /
            ACME_LINE=$(grep -n "\.well-known/acme-challenge" "$CONFIG_FILE" | head -1 | cut -d: -f1)
            LOCATION_LINE=$(grep -n "location / {" "$CONFIG_FILE" | head -1 | cut -d: -f1)
            
            if [ -n "$ACME_LINE" ] && [ -n "$LOCATION_LINE" ] && [ "$ACME_LINE" -lt "$LOCATION_LINE" ]; then
                log "   ✓ ACME challenge находится ПЕРЕД location / (правильно)"
            else
                warn "   ⚠️  ACME challenge находится ПОСЛЕ location / (неправильно!)"
                warn "      Нужно переместить секцию ACME challenge ПЕРЕД location /"
            fi
        else
            error "   ✗ Секция ACME challenge НЕ найдена!"
        fi
    else
        error "   ✗ Порт 80 не настроен"
    fi
fi

echo ""

# 3. Проверка конфигов для admin.maskbrowser.ru
info "3. Проверка конфигурации admin.maskbrowser.ru..."

ADMIN_CONFIG_FILE="/etc/nginx/sites-available/admin.maskbrowser.ru.conf"

if [ ! -f "$ADMIN_CONFIG_FILE" ]; then
    error "   ✗ Файл $ADMIN_CONFIG_FILE не найден"
else
    log "   ✓ Файл найден: $ADMIN_CONFIG_FILE"
    
    # Проверка HTTP блока (порт 80)
    echo ""
    info "   HTTP блок (порт 80):"
    if grep -q "listen 80" "$ADMIN_CONFIG_FILE"; then
        log "   ✓ Порт 80 настроен"
        
        # Показать блок server для порта 80
        echo "   Содержимое HTTP блока:"
        sed -n '/server {/,/^}/p' "$ADMIN_CONFIG_FILE" | head -20 | sed 's/^/   /'
        
        # Проверка ACME challenge
        if grep -q "\.well-known/acme-challenge" "$ADMIN_CONFIG_FILE"; then
            log "   ✓ Секция ACME challenge найдена"
            
            # Проверить порядок
            ACME_LINE=$(grep -n "\.well-known/acme-challenge" "$ADMIN_CONFIG_FILE" | head -1 | cut -d: -f1)
            LOCATION_LINE=$(grep -n "location / {" "$ADMIN_CONFIG_FILE" | head -1 | cut -d: -f1)
            
            if [ -n "$ACME_LINE" ] && [ -n "$LOCATION_LINE" ] && [ "$ACME_LINE" -lt "$LOCATION_LINE" ]; then
                log "   ✓ ACME challenge находится ПЕРЕД location / (правильно)"
            else
                warn "   ⚠️  ACME challenge находится ПОСЛЕ location / (неправильно!)"
            fi
        else
            error "   ✗ Секция ACME challenge НЕ найдена!"
        fi
    else
        error "   ✗ Порт 80 не настроен"
    fi
fi

echo ""

# 4. Проверка директории webroot
info "4. Проверка директории webroot..."

WEBROOT_DIR="/var/www/certbot"

if [ -d "$WEBROOT_DIR" ]; then
    log "   ✓ Директория существует: $WEBROOT_DIR"
    
    # Проверка прав доступа
    OWNER=$(stat -c '%U:%G' "$WEBROOT_DIR" 2>/dev/null || stat -f '%Su:%Sg' "$WEBROOT_DIR" 2>/dev/null || echo "unknown")
    PERMS=$(stat -c '%a' "$WEBROOT_DIR" 2>/dev/null || stat -f '%A' "$WEBROOT_DIR" 2>/dev/null || echo "unknown")
    
    log "   Владелец: $OWNER, Права: $PERMS"
    
    if [ "$OWNER" = "www-data:www-data" ] || [ "$OWNER" = "root:root" ]; then
        log "   ✓ Права доступа корректны"
    else
        warn "   ⚠️  Владелец не www-data:www-data (может быть проблемой)"
    fi
else
    error "   ✗ Директория не существует: $WEBROOT_DIR"
    log "   Создайте: sudo mkdir -p $WEBROOT_DIR && sudo chown www-data:www-data $WEBROOT_DIR"
fi

echo ""

# 5. Проверка активных конфигов
info "5. Проверка активных конфигов (sites-enabled)..."

ENABLED_CONFIGS=$(ls -la /etc/nginx/sites-enabled/ 2>/dev/null | grep -v "^total" | awk '{print $9, $10, $11}' | grep -v "^$")

if [ -n "$ENABLED_CONFIGS" ]; then
    echo "   Активные конфиги:"
    echo "$ENABLED_CONFIGS" | sed 's/^/   /'
    
    # Проверка на конфликты
    if echo "$ENABLED_CONFIGS" | grep -q "default"; then
        warn "   ⚠️  Найден default конфиг - может быть конфликт"
    fi
else
    warn "   ⚠️  Нет активных конфигов в sites-enabled"
fi

echo ""

# 6. Тест доступности ACME challenge
info "6. Тест доступности ACME challenge..."

# Создать тестовый файл
TEST_FILE="$WEBROOT_DIR/nginx-test-$$.txt"
echo "test-$(date +%s)" | sudo tee "$TEST_FILE" > /dev/null

for domain in "maskbrowser.ru" "www.maskbrowser.ru" "admin.maskbrowser.ru"; do
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://$domain/.well-known/acme-challenge/nginx-test-$$.txt" 2>/dev/null || echo "000")
    
    if [ "$HTTP_CODE" = "200" ]; then
        CONTENT=$(curl -s "http://$domain/.well-known/acme-challenge/nginx-test-$$.txt" 2>/dev/null || echo "")
        if [ -n "$CONTENT" ]; then
            log "   ✓ http://$domain/.well-known/acme-challenge/ доступен (HTTP 200)"
        else
            warn "   ⚠️  http://$domain/.well-known/acme-challenge/ возвращает 200, но без содержимого"
        fi
    elif [ "$HTTP_CODE" = "301" ] || [ "$HTTP_CODE" = "302" ]; then
        warn "   ⚠️  http://$domain/.well-known/acme-challenge/ перенаправляет (HTTP $HTTP_CODE)"
        warn "      Это проблема! ACME challenge должен работать на HTTP, не перенаправлять на HTTPS"
    elif [ "$HTTP_CODE" = "404" ]; then
        error "   ✗ http://$domain/.well-known/acme-challenge/ недоступен (HTTP 404)"
    else
        warn "   ⚠️  http://$domain/.well-known/acme-challenge/ недоступен (HTTP $HTTP_CODE)"
    fi
done

# Удалить тестовый файл
sudo rm -f "$TEST_FILE"

echo ""

# 7. Показать полную конфигурацию для порта 80
info "7. Полная конфигурация HTTP блоков:"

echo ""
echo "=== maskbrowser.ru (HTTP блок) ==="
if [ -f "$CONFIG_FILE" ]; then
    awk '/server {/,/^}/' "$CONFIG_FILE" | grep -A 100 "listen 80" | head -30
else
    echo "Файл не найден"
fi

echo ""
echo "=== admin.maskbrowser.ru (HTTP блок) ==="
if [ -f "$ADMIN_CONFIG_FILE" ]; then
    awk '/server {/,/^}/' "$ADMIN_CONFIG_FILE" | grep -A 100 "listen 80" | head -30
else
    echo "Файл не найден"
fi

echo ""
log "=== Диагностика завершена ==="
echo ""
log "Если есть проблемы:"
log "  1. Убедитесь, что секция ACME challenge находится ПЕРЕД location /"
log "  2. Проверьте, что директория /var/www/certbot существует и доступна"
log "  3. Убедитесь, что нет конфликтов с другими конфигами"
