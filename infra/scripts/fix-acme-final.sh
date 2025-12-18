#!/bin/bash

# Финальное исправление проблемы с ACME challenge

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

log "=== Финальное исправление ACME challenge ==="
echo ""

# 1. Проверить текущую конфигурацию
log "1. Проверка текущей конфигурации..."

CONFIG_FILES=(
    "/etc/nginx/sites-available/maskbrowser.ru.conf"
    "/etc/nginx/sites-available/admin.maskbrowser.ru.conf"
)

for CONFIG_FILE in "${CONFIG_FILES[@]}"; do
    if [ ! -f "$CONFIG_FILE" ]; then
        warn "   Файл не найден: $CONFIG_FILE"
        continue
    fi
    
    log "   Проверка: $CONFIG_FILE"
    
    # Показать текущую секцию ACME challenge
    echo "   Текущая секция ACME challenge:"
    grep -A 3 "acme-challenge" "$CONFIG_FILE" | sed 's/^/   /'
    
    # Проверить, используется ли alias
    if grep -q "alias /var/www/certbot" "$CONFIG_FILE"; then
        log "   ✓ Используется alias"
    else
        warn "   ⚠️  Не используется alias, исправляем..."
        
        # Создать резервную копию
        cp "$CONFIG_FILE" "$CONFIG_FILE.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Заменить root на alias
        sed -i 's|root /var/www/certbot;|alias /var/www/certbot;|g' "$CONFIG_FILE"
        
        log "   ✓ Заменено на alias"
    fi
done

echo ""

# 2. Убедиться, что секция ACME challenge правильная
log "2. Проверка и исправление секции ACME challenge..."

for CONFIG_FILE in "${CONFIG_FILES[@]}"; do
    if [ ! -f "$CONFIG_FILE" ]; then
        continue
    fi
    
    # Проверить, есть ли try_files
    if ! grep -A 3 "acme-challenge" "$CONFIG_FILE" | grep -q "try_files"; then
        warn "   Добавляем try_files в $CONFIG_FILE"
        
        # Создать резервную копию
        cp "$CONFIG_FILE" "$CONFIG_FILE.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Добавить try_files после alias
        sed -i '/alias \/var\/www\/certbot;/a\        try_files $uri =404;' "$CONFIG_FILE"
        
        log "   ✓ Добавлен try_files"
    fi
done

echo ""

# 3. Проверить порядок location блоков
log "3. Проверка порядка location блоков..."

for CONFIG_FILE in "${CONFIG_FILES[@]}"; do
    if [ ! -f "$CONFIG_FILE" ]; then
        continue
    fi
    
    # Найти номера строк
    ACME_LINE=$(grep -n "acme-challenge" "$CONFIG_FILE" | head -1 | cut -d: -f1)
    LOCATION_LINE=$(grep -n "^    location / {" "$CONFIG_FILE" | head -1 | cut -d: -f1)
    
    if [ -n "$ACME_LINE" ] && [ -n "$LOCATION_LINE" ]; then
        if [ "$ACME_LINE" -lt "$LOCATION_LINE" ]; then
            log "   ✓ $CONFIG_FILE: порядок правильный (ACME перед location /)"
        else
            warn "   ⚠️  $CONFIG_FILE: неправильный порядок!"
            warn "      ACME challenge должен быть ПЕРЕД location /"
        fi
    fi
done

echo ""

# 4. Альтернативное решение: использовать более специфичный location
log "4. Применение альтернативного решения..."

for CONFIG_FILE in "${CONFIG_FILES[@]}"; do
    if [ ! -f "$CONFIG_FILE" ]; then
        continue
    fi
    
    # Проверить, есть ли уже правильная секция
    if grep -A 4 "acme-challenge" "$CONFIG_FILE" | grep -q "try_files"; then
        log "   ✓ $CONFIG_FILE: конфигурация правильная"
        continue
    fi
    
    warn "   Исправляем $CONFIG_FILE..."
    
    # Создать резервную копию
    cp "$CONFIG_FILE" "$CONFIG_FILE.backup.$(date +%Y%m%d_%H%M%S)"
    
    # Заменить всю секцию ACME challenge на правильную
    sed -i '/location \^~ \/\.well-known\/acme-challenge\/ {/,/}/c\
    location ^~ /.well-known/acme-challenge/ {\
        alias /var/www/certbot;\
        try_files $uri =404;\
        access_log off;\
    }' "$CONFIG_FILE"
    
    log "   ✓ Секция заменена"
done

echo ""

# 5. Проверить синтаксис
log "5. Проверка синтаксиса nginx..."
if nginx -t 2>&1 | grep -q "successful"; then
    log "   ✓ Синтаксис nginx корректен"
else
    error "   ✗ Ошибка в синтаксисе nginx:"
    nginx -t
    exit 1
fi

# 6. Перезагрузить nginx
log "6. Перезагрузка nginx..."
systemctl reload nginx || error "Не удалось перезагрузить nginx"
log "   ✓ Nginx перезагружен"

echo ""

# 7. Финальный тест
log "7. Финальный тест..."

# Создать тестовый файл
TEST_FILE="/var/www/certbot/test-final.txt"
echo "test-final-$(date +%s)" > "$TEST_FILE"
chown www-data:www-data "$TEST_FILE"
chmod 644 "$TEST_FILE"

sleep 2

for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    TEST_URL="http://$domain/.well-known/acme-challenge/test-final.txt"
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$TEST_URL" 2>/dev/null || echo "000")
    HTTP_BODY=$(curl -s "$TEST_URL" 2>/dev/null || echo "")
    
    if [ "$HTTP_CODE" = "200" ] && echo "$HTTP_BODY" | grep -q "test-final"; then
        log "   ✓ $domain: ACME challenge работает! (HTTP 200)"
    elif [ "$HTTP_CODE" = "301" ] || [ "$HTTP_CODE" = "302" ]; then
        warn "   ⚠️  $domain: все еще перенаправление (HTTP $HTTP_CODE)"
        warn "      Возможно, нужно проверить другие location блоки"
        
        # Показать все location блоки
        echo "   Все location блоки в конфиге:"
        if [ "$domain" = "maskbrowser.ru" ]; then
            grep -n "location" /etc/nginx/sites-available/maskbrowser.ru.conf | sed 's/^/   /'
        else
            grep -n "location" /etc/nginx/sites-available/admin.maskbrowser.ru.conf | sed 's/^/   /'
        fi
    else
        warn "   ⚠️  $domain: неожиданный ответ (HTTP $HTTP_CODE)"
    fi
done

rm -f "$TEST_FILE"

echo ""
log "=== Исправление завершено ==="
echo ""
log "Если проблемы сохраняются:"
log "  1. Проверьте все location блоки в конфигах"
log "  2. Убедитесь, что нет других location блоков, которые перехватывают запрос"
log "  3. Проверьте логи: sudo tail -f /var/log/nginx/error.log"
log "  4. Попробуйте использовать точное совпадение: location = /.well-known/acme-challenge/"

