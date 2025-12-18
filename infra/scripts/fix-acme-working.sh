#!/bin/bash

# Рабочее исправление проблемы с ACME challenge

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

if [ "$EUID" -ne 0 ]; then
    error "Пожалуйста, запустите скрипт с правами root или через sudo"
    exit 1
fi

log "=== Исправление ACME challenge (рабочее решение) ==="
echo ""

# Проблема: try_files $uri =404 не работает с alias
# Решение: убрать try_files или использовать другой подход

CONFIG_FILES=(
    "/etc/nginx/sites-available/maskbrowser.ru.conf"
    "/etc/nginx/sites-available/admin.maskbrowser.ru.conf"
)

for CONFIG_FILE in "${CONFIG_FILES[@]}"; do
    if [ ! -f "$CONFIG_FILE" ]; then
        warn "Файл не найден: $CONFIG_FILE"
        continue
    fi
    
    log "Исправление: $CONFIG_FILE"
    
    # Создать резервную копию
    cp "$CONFIG_FILE" "$CONFIG_FILE.backup.$(date +%Y%m%d_%H%M%S)"
    
    # Заменить секцию ACME challenge - убрать try_files, так как он не работает с alias
    sed -i '/location \/\.well-known\/acme-challenge\/ {/,/}/c\
    location /.well-known/acme-challenge/ {\
        alias /var/www/certbot;\
        default_type text/plain;\
        access_log off;\
    }' "$CONFIG_FILE"
    
    log "   ✓ Секция исправлена (убран try_files)"
done

echo ""

# Проверить синтаксис
log "Проверка синтаксиса nginx..."
if nginx -t 2>&1 | grep -q "successful"; then
    log "   ✓ Синтаксис корректен"
else
    error "   ✗ Ошибка в синтаксисе:"
    nginx -t
    exit 1
fi

# Перезагрузить nginx
log "Перезагрузка nginx..."
systemctl reload nginx || error "Не удалось перезагрузить nginx"
log "   ✓ Nginx перезагружен"

echo ""

# Создать тестовый файл
log "Создание тестового файла..."
mkdir -p /var/www/certbot
echo "test-$(date +%s)" | tee /var/www/certbot/test.txt > /dev/null
chown www-data:www-data /var/www/certbot/test.txt
chmod 644 /var/www/certbot/test.txt
log "   ✓ Тестовый файл создан"

sleep 2

# Тест
log "Тест доступности..."
for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://$domain/.well-known/acme-challenge/test.txt" 2>/dev/null || echo "000")
    HTTP_BODY=$(curl -s "http://$domain/.well-known/acme-challenge/test.txt" 2>/dev/null || echo "")
    
    if [ "$HTTP_CODE" = "200" ] && [ -n "$HTTP_BODY" ]; then
        log "   ✓ $domain: работает! (HTTP 200, содержимое: $HTTP_BODY)"
    else
        warn "   ⚠️  $domain: все еще проблемы (HTTP $HTTP_CODE)"
    fi
done

rm -f /var/www/certbot/test.txt

echo ""
log "=== Исправление завершено ==="
log ""
log "Если все еще не работает, попробуйте использовать standalone режим certbot:"
log "  sudo systemctl stop nginx"
log "  sudo certbot certonly --standalone -d maskbrowser.ru -d www.maskbrowser.ru"
log "  sudo certbot certonly --standalone -d admin.maskbrowser.ru"
log "  sudo systemctl start nginx"

