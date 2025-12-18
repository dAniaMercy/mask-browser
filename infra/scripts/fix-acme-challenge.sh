#!/bin/bash

# Скрипт для исправления конфигурации ACME challenge в nginx

set -e

# Цвета
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1"
    exit 1
}

if [ "$EUID" -ne 0 ]; then
    error "Пожалуйста, запустите скрипт с правами root или через sudo"
fi

log "=== Исправление конфигурации ACME challenge ==="
echo ""

# 1. Создать директорию для webroot
log "1. Создание директории для webroot..."
mkdir -p /var/www/certbot
chown -R www-data:www-data /var/www/certbot
chmod -R 755 /var/www/certbot
log "   ✓ Директория /var/www/certbot создана"

# 2. Проверить конфигурацию nginx для maskbrowser.ru
log "2. Проверка конфигурации nginx для maskbrowser.ru..."

CONFIG_FILE="/etc/nginx/sites-available/maskbrowser.ru.conf"

if [ -f "$CONFIG_FILE" ]; then
    # Проверяем, есть ли секция для ACME challenge
    if grep -q "\.well-known/acme-challenge" "$CONFIG_FILE"; then
        log "   ✓ Секция ACME challenge найдена в конфиге"
    else
        warn "   ✗ Секция ACME challenge не найдена, добавляем..."
        
        # Создаем резервную копию
        cp "$CONFIG_FILE" "$CONFIG_FILE.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Добавляем секцию для ACME challenge в HTTP блок (перед redirect)
        sed -i '/location \/ {/,/return 301/s/^/# ACME challenge\n    location ^~ \/.well-known\/acme-challenge\/ {\n        root \/var\/www\/certbot;\n    }\n\n    # Redirect all other traffic to HTTPS\n/' "$CONFIG_FILE"
        
        # Или проще - добавить перед location /
        if ! grep -q "location ^~ /.well-known/acme-challenge/" "$CONFIG_FILE"; then
            # Находим блок server для порта 80 и добавляем перед location /
            sed -i '/server_name maskbrowser.ru www.maskbrowser.ru;/,/location \/ {/{
                /location \/ {/i\
    # Let'\''s Encrypt ACME challenge\
    location ^~ /.well-known/acme-challenge/ {\
        root /var/www/certbot;\
    }\
\
' "$CONFIG_FILE"
        fi
        
        log "   ✓ Секция ACME challenge добавлена"
    fi
else
    warn "   Файл $CONFIG_FILE не найден"
fi

# 3. Проверить конфигурацию nginx для admin.maskbrowser.ru
log "3. Проверка конфигурации nginx для admin.maskbrowser.ru..."

ADMIN_CONFIG_FILE="/etc/nginx/sites-available/admin.maskbrowser.ru.conf"

if [ -f "$ADMIN_CONFIG_FILE" ]; then
    # Проверяем, есть ли секция для ACME challenge
    if grep -q "\.well-known/acme-challenge" "$ADMIN_CONFIG_FILE"; then
        log "   ✓ Секция ACME challenge найдена в конфиге"
    else
        warn "   ✗ Секция ACME challenge не найдена, добавляем..."
        
        # Создаем резервную копию
        cp "$ADMIN_CONFIG_FILE" "$ADMIN_CONFIG_FILE.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Добавляем секцию для ACME challenge
        if ! grep -q "location ^~ /.well-known/acme-challenge/" "$ADMIN_CONFIG_FILE"; then
            sed -i '/server_name admin.maskbrowser.ru;/,/location \/ {/{
                /location \/ {/i\
    # Let'\''s Encrypt ACME challenge\
    location ^~ /.well-known/acme-challenge/ {\
        root /var/www/certbot;\
    }\
\
' "$ADMIN_CONFIG_FILE"
        fi
        
        log "   ✓ Секция ACME challenge добавлена"
    fi
else
    warn "   Файл $ADMIN_CONFIG_FILE не найден"
fi

# 4. Проверить синтаксис nginx
log "4. Проверка синтаксиса nginx..."
if nginx -t 2>&1 | grep -q "successful"; then
    log "   ✓ Синтаксис nginx корректен"
else
    error "   ✗ Ошибка в конфигурации nginx"
    nginx -t
fi

# 5. Перезагрузить nginx
log "5. Перезагрузка nginx..."
systemctl reload nginx || error "Не удалось перезагрузить nginx"
log "   ✓ Nginx перезагружен"

# 6. Проверить доступность ACME challenge
log "6. Проверка доступности ACME challenge..."

# Создаем тестовый файл
TEST_FILE="/var/www/certbot/test.txt"
echo "test" > "$TEST_FILE"

for domain in "maskbrowser.ru" "www.maskbrowser.ru" "admin.maskbrowser.ru"; do
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://$domain/.well-known/acme-challenge/test.txt" || echo "000")
    
    if [ "$HTTP_CODE" = "200" ]; then
        log "   ✓ http://$domain/.well-known/acme-challenge/ доступен"
    else
        warn "   ✗ http://$domain/.well-known/acme-challenge/ недоступен (HTTP $HTTP_CODE)"
    fi
done

# Удаляем тестовый файл
rm -f "$TEST_FILE"

echo ""
log "=== Исправление завершено ==="
echo ""
log "Теперь можно создать сертификаты:"
log "  sudo certbot certonly --webroot --webroot-path=/var/www/certbot --email admin@maskbrowser.ru --agree-tos --no-eff-email -d maskbrowser.ru -d www.maskbrowser.ru"
log "  sudo certbot certonly --webroot --webroot-path=/var/www/certbot --email admin@maskbrowser.ru --agree-tos --no-eff-email -d admin.maskbrowser.ru"
