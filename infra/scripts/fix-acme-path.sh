#!/bin/bash

# Скрипт для исправления проблемы с путем ACME challenge

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

log "=== Исправление пути ACME challenge ==="
echo ""

# 1. Создать и настроить директорию
log "1. Создание и настройка директории /var/www/certbot..."
mkdir -p /var/www/certbot
chown -R www-data:www-data /var/www/certbot
chmod -R 755 /var/www/certbot
log "   ✓ Директория создана и настроена"

# 2. Проверить текущую конфигурацию
log "2. Проверка текущей конфигурации..."

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
    
    # Проверить, используется ли root или alias
    if grep -q "root /var/www/certbot" "$CONFIG_FILE"; then
        log "   ✓ Используется root /var/www/certbot"
        
        # Попробовать изменить на alias (более надежно)
        # Но сначала проверим, может быть проблема в другом
        
    elif grep -q "alias" "$CONFIG_FILE"; then
        log "   ✓ Используется alias"
    fi
done

echo ""

# 3. Тест создания и чтения файла
log "3. Тест создания и чтения файла..."

TEST_FILE="/var/www/certbot/test-$(date +%s).txt"
TEST_CONTENT="test-$(date +%s)"

echo "$TEST_CONTENT" > "$TEST_FILE"
chown www-data:www-data "$TEST_FILE"
chmod 644 "$TEST_FILE"

log "   Создан тестовый файл: $TEST_FILE"
log "   Содержимое: $TEST_CONTENT"

# Проверить, может ли nginx прочитать файл
if [ -f "$TEST_FILE" ] && [ -r "$TEST_FILE" ]; then
    FILE_CONTENT=$(cat "$TEST_FILE")
    if [ "$FILE_CONTENT" = "$TEST_CONTENT" ]; then
        log "   ✓ Файл читается корректно"
    else
        warn "   ⚠️  Проблема с чтением файла"
    fi
else
    error "   ✗ Файл не существует или недоступен для чтения"
fi

# Проверить через HTTP
log "4. Проверка доступности через HTTP..."

for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    TEST_URL="http://$domain/.well-known/acme-challenge/$(basename $TEST_FILE)"
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$TEST_URL" 2>/dev/null || echo "000")
    HTTP_BODY=$(curl -s "$TEST_URL" 2>/dev/null || echo "")
    
    if [ "$HTTP_CODE" = "200" ] && [ "$HTTP_BODY" = "$TEST_CONTENT" ]; then
        log "   ✓ $domain: файл доступен (HTTP 200)"
    elif [ "$HTTP_CODE" = "301" ] || [ "$HTTP_CODE" = "302" ]; then
        warn "   ⚠️  $domain: перенаправление (HTTP $HTTP_CODE)"
        warn "      Это означает, что nginx не находит файл и выполняет redirect"
        
        # Попробовать использовать alias вместо root
        log "   Попытка исправить через использование alias..."
        
        if [ "$domain" = "maskbrowser.ru" ]; then
            CONFIG_FILE="/etc/nginx/sites-available/maskbrowser.ru.conf"
        else
            CONFIG_FILE="/etc/nginx/sites-available/admin.maskbrowser.ru.conf"
        fi
        
        # Создать резервную копию
        cp "$CONFIG_FILE" "$CONFIG_FILE.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Заменить root на alias
        sed -i 's|root /var/www/certbot;|alias /var/www/certbot;|g' "$CONFIG_FILE"
        
        log "   ✓ Заменено root на alias в $CONFIG_FILE"
        
    elif [ "$HTTP_CODE" = "404" ]; then
        error "   ✗ $domain: файл не найден (HTTP 404)"
    else
        warn "   ⚠️  $domain: неожиданный ответ (HTTP $HTTP_CODE)"
    fi
done

# Удалить тестовый файл
rm -f "$TEST_FILE"

echo ""

# 5. Проверить синтаксис nginx
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

FINAL_TEST_FILE="/var/www/certbot/final-test.txt"
echo "final-test" > "$FINAL_TEST_FILE"
chown www-data:www-data "$FINAL_TEST_FILE"
chmod 644 "$FINAL_TEST_FILE"

sleep 2

for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    FINAL_URL="http://$domain/.well-known/acme-challenge/final-test.txt"
    FINAL_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$FINAL_URL" 2>/dev/null || echo "000")
    FINAL_BODY=$(curl -s "$FINAL_URL" 2>/dev/null || echo "")
    
    if [ "$FINAL_CODE" = "200" ] && echo "$FINAL_BODY" | grep -q "final-test"; then
        log "   ✓ $domain: ACME challenge работает!"
    else
        warn "   ⚠️  $domain: все еще проблемы (HTTP $FINAL_CODE)"
        warn "      Проверьте конфигурацию вручную"
    fi
done

rm -f "$FINAL_TEST_FILE"

echo ""
log "=== Исправление завершено ==="
echo ""
log "Если проблемы сохраняются, попробуйте:"
log "  1. Использовать alias вместо root в конфигах"
log "  2. Проверить права доступа: ls -la /var/www/certbot"
log "  3. Проверить логи: sudo tail -f /var/log/nginx/error.log"

