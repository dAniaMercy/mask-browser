#!/bin/bash

# Скрипт для отладки проблемы с ACME challenge

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

log "=== Отладка проблемы с ACME challenge ==="
echo ""

# 1. Показать текущую конфигурацию ACME challenge
log "1. Текущая конфигурация ACME challenge:"
echo ""

echo "=== maskbrowser.ru ==="
if [ -f "/etc/nginx/sites-available/maskbrowser.ru.conf" ]; then
    echo "HTTP блок (порт 80):"
    awk '/server {/,/^}/' /etc/nginx/sites-available/maskbrowser.ru.conf | grep -A 30 "listen 80" | head -40
    echo ""
    echo "Секция ACME challenge:"
    grep -A 5 "acme-challenge" /etc/nginx/sites-available/maskbrowser.ru.conf || echo "НЕ НАЙДЕНА"
else
    error "Файл не найден"
fi

echo ""
echo "=== admin.maskbrowser.ru ==="
if [ -f "/etc/nginx/sites-available/admin.maskbrowser.ru.conf" ]; then
    echo "HTTP блок (порт 80):"
    awk '/server {/,/^}/' /etc/nginx/sites-available/admin.maskbrowser.ru.conf | grep -A 30 "listen 80" | head -40
    echo ""
    echo "Секция ACME challenge:"
    grep -A 5 "acme-challenge" /etc/nginx/sites-available/admin.maskbrowser.ru.conf || echo "НЕ НАЙДЕНА"
else
    error "Файл не найден"
fi

echo ""

# 2. Показать все location блоки
log "2. Все location блоки в конфигах:"
echo ""
echo "=== maskbrowser.ru ==="
grep -n "location" /etc/nginx/sites-available/maskbrowser.ru.conf | sed 's/^/   /'
echo ""
echo "=== admin.maskbrowser.ru ==="
grep -n "location" /etc/nginx/sites-available/admin.maskbrowser.ru.conf | sed 's/^/   /'

echo ""

# 3. Проверить файл
log "3. Проверка тестового файла:"
TEST_FILE="/var/www/certbot/test.txt"
if [ -f "$TEST_FILE" ]; then
    log "   ✓ Файл существует: $TEST_FILE"
    log "   Содержимое: $(cat $TEST_FILE)"
    log "   Владелец: $(stat -c '%U:%G' $TEST_FILE 2>/dev/null || stat -f '%Su:%Sg' $TEST_FILE 2>/dev/null)"
    log "   Права: $(stat -c '%a' $TEST_FILE 2>/dev/null || stat -f '%A' $TEST_FILE 2>/dev/null)"
    
    # Проверить, может ли www-data прочитать
    if sudo -u www-data test -r "$TEST_FILE"; then
        log "   ✓ www-data может прочитать файл"
    else
        error "   ✗ www-data НЕ может прочитать файл"
    fi
else
    warn "   Файл не существует: $TEST_FILE"
fi

echo ""

# 4. Проверить директорию
log "4. Проверка директории /var/www/certbot:"
if [ -d "/var/www/certbot" ]; then
    log "   ✓ Директория существует"
    log "   Владелец: $(stat -c '%U:%G' /var/www/certbot 2>/dev/null || stat -f '%Su:%Sg' /var/www/certbot 2>/dev/null)"
    log "   Права: $(stat -c '%a' /var/www/certbot 2>/dev/null || stat -f '%A' /var/www/certbot 2>/dev/null)"
    log "   Содержимое:"
    ls -la /var/www/certbot | head -10 | sed 's/^/   /'
else
    error "   ✗ Директория не существует"
fi

echo ""

# 5. Тест через curl с подробным выводом
log "5. Тест через curl:"
echo ""
for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    log "   Тест для $domain:"
    echo "   URL: http://$domain/.well-known/acme-challenge/test.txt"
    echo ""
    echo "   Ответ:"
    curl -v "http://$domain/.well-known/acme-challenge/test.txt" 2>&1 | grep -E "(< HTTP|Location:|301|302|200|404)" | sed 's/^/   /'
    echo ""
done

echo ""

# 6. Проверить логи nginx
log "6. Последние записи в логах nginx:"
echo ""
tail -20 /var/log/nginx/error.log 2>/dev/null | grep -E "(acme|challenge|test.txt)" | tail -5 | sed 's/^/   /' || echo "   Нет записей"

echo ""
log "=== Отладка завершена ==="

