#!/bin/bash

# Скрипт для исправления проблемы с default_server в nginx

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

log "=== Исправление проблемы с default_server ==="
echo ""

# 1. Проверить все конфиги на default_server
log "1. Поиск default_server в конфигах..."

ALL_CONFIGS=$(find /etc/nginx/sites-available -name "*.conf" 2>/dev/null)

FOUND_DEFAULT=false
for config in $ALL_CONFIGS; do
    if grep -q "default_server" "$config"; then
        warn "   Найден default_server в: $config"
        grep "default_server" "$config" | sed 's/^/      /'
        FOUND_DEFAULT=true
    fi
done

if [ "$FOUND_DEFAULT" = false ]; then
    log "   ✓ default_server не найден в конфигах"
else
    warn "   ⚠️  Найден default_server - это может быть проблемой"
fi

echo ""

# 2. Показать полную конфигурацию nginx для порта 443
log "2. Полная конфигурация nginx для порта 443:"
echo ""
sudo nginx -T 2>/dev/null | grep -A 15 "listen 443" | head -50

echo ""
echo ""

# 3. Проверить порядок загрузки конфигов
log "3. Порядок загрузки конфигов:"
echo ""
ls -la /etc/nginx/sites-enabled/ | grep -v "^total" | awk '{print $9, $10, $11}' | sed 's/^/   /'

echo ""

# 4. Проверить, какой server блок используется для maskbrowser.ru
log "4. Проверка server блоков для maskbrowser.ru:"
echo ""
sudo nginx -T 2>/dev/null | grep -B 5 -A 15 "server_name.*maskbrowser.ru" | head -40

echo ""
echo ""

# 5. Проверить wbmoneyback.ru конфиг
log "5. Проверка конфига wbmoneyback.ru:"
WBMONEYBACK_CONFIG="/etc/nginx/sites-available/wbmoneyback.ru"
if [ -f "$WBMONEYBACK_CONFIG" ]; then
    echo "   Содержимое HTTP и HTTPS блоков:"
    grep -A 20 "listen 80\|listen 443" "$WBMONEYBACK_CONFIG" | head -40 | sed 's/^/   /'
    
    if grep -q "default_server" "$WBMONEYBACK_CONFIG"; then
        warn "   ⚠️  В wbmoneyback.ru найден default_server!"
        log "   Удаление default_server..."
        
        # Создать резервную копию
        cp "$WBMONEYBACK_CONFIG" "$WBMONEYBACK_CONFIG.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Удалить default_server
        sed -i 's/ listen 443 ssl default_server;/ listen 443 ssl;/g' "$WBMONEYBACK_CONFIG"
        sed -i 's/ listen \[::\]:443 ssl default_server;/ listen [::]:443 ssl;/g' "$WBMONEYBACK_CONFIG"
        sed -i 's/ listen 443 ssl http2 default_server;/ listen 443 ssl http2;/g' "$WBMONEYBACK_CONFIG"
        sed -i 's/ listen \[::\]:443 ssl http2 default_server;/ listen [::]:443 ssl http2;/g' "$WBMONEYBACK_CONFIG"
        sed -i 's/ listen 80 default_server;/ listen 80;/g' "$WBMONEYBACK_CONFIG"
        sed -i 's/ listen \[::\]:80 default_server;/ listen [::]:80;/g' "$WBMONEYBACK_CONFIG"
        
        log "   ✓ default_server удален"
    fi
else
    warn "   Файл не найден: $WBMONEYBACK_CONFIG"
fi

echo ""

# 6. Проверить синтаксис и перезагрузить
log "6. Проверка синтаксиса nginx..."
if nginx -t 2>&1 | grep -q "successful"; then
    log "   ✓ Синтаксис корректен"
    
    log "   Перезагрузка nginx..."
    systemctl reload nginx || error "Не удалось перезагрузить nginx"
    log "   ✓ Nginx перезагружен"
else
    error "   ✗ Ошибка в синтаксисе:"
    nginx -t
    exit 1
fi

echo ""

# 7. Проверить SSL после исправления
log "7. Проверка SSL после исправления..."
sleep 2

for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    log "   Проверка $domain..."
    
    CERT_CN=$(echo | timeout 5 openssl s_client -connect "$domain:443" -servername "$domain" 2>/dev/null | grep "subject=CN" | sed 's/.*CN=\([^,]*\).*/\1/' || echo "")
    
    if echo "$CERT_CN" | grep -q "$domain"; then
        log "   ✓ Используется правильный сертификат (CN: $CERT_CN)"
    elif echo "$CERT_CN" | grep -q "wbmoneyback.ru"; then
        warn "   ⚠️  Все еще используется сертификат от wbmoneyback.ru"
        warn "      Возможно, нужно проверить порядок server блоков"
    else
        warn "   ⚠️  Неожиданный CN: $CERT_CN"
    fi
done

echo ""
log "=== Исправление завершено ==="

