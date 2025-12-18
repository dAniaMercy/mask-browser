#!/bin/bash

# Скрипт для исправления SSL сертификатов в nginx

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

log "=== Исправление SSL сертификатов в nginx ==="
echo ""

# 1. Проверить наличие сертификатов
log "1. Проверка наличия SSL сертификатов..."

if [ -f "/etc/letsencrypt/live/maskbrowser.ru/fullchain.pem" ]; then
    log "   ✓ Сертификат для maskbrowser.ru найден"
else
    error "   ✗ Сертификат для maskbrowser.ru не найден"
    exit 1
fi

if [ -f "/etc/letsencrypt/live/admin.maskbrowser.ru/fullchain.pem" ]; then
    log "   ✓ Сертификат для admin.maskbrowser.ru найден"
else
    error "   ✗ Сертификат для admin.maskbrowser.ru не найден"
    exit 1
fi

echo ""

# 2. Проверить конфигурацию nginx
log "2. Проверка конфигурации nginx..."

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
    
    # Проверить пути к сертификатам
    if [ "$CONFIG_FILE" = "/etc/nginx/sites-available/maskbrowser.ru.conf" ]; then
        EXPECTED_CERT="/etc/letsencrypt/live/maskbrowser.ru/fullchain.pem"
        EXPECTED_KEY="/etc/letsencrypt/live/maskbrowser.ru/privkey.pem"
    else
        EXPECTED_CERT="/etc/letsencrypt/live/admin.maskbrowser.ru/fullchain.pem"
        EXPECTED_KEY="/etc/letsencrypt/live/admin.maskbrowser.ru/privkey.pem"
    fi
    
    CURRENT_CERT=$(grep "ssl_certificate" "$CONFIG_FILE" | grep -v "#" | head -1 | awk '{print $2}' | sed 's/;//')
    CURRENT_KEY=$(grep "ssl_certificate_key" "$CONFIG_FILE" | grep -v "#" | head -1 | awk '{print $2}' | sed 's/;//')
    
    if [ "$CURRENT_CERT" = "$EXPECTED_CERT" ]; then
        log "   ✓ Путь к сертификату правильный: $CURRENT_CERT"
    else
        warn "   ⚠️  Неправильный путь к сертификату: $CURRENT_CERT"
        warn "      Ожидается: $EXPECTED_CERT"
        
        # Исправить
        log "   Исправление пути к сертификату..."
        cp "$CONFIG_FILE" "$CONFIG_FILE.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Заменить пути к сертификатам
        sed -i "s|ssl_certificate.*|ssl_certificate $EXPECTED_CERT;|g" "$CONFIG_FILE"
        sed -i "s|ssl_certificate_key.*|ssl_certificate_key $EXPECTED_KEY;|g" "$CONFIG_FILE"
        
        # Если есть ssl_trusted_certificate, обновить его тоже
        if grep -q "ssl_trusted_certificate" "$CONFIG_FILE"; then
            TRUSTED_CERT=$(echo "$EXPECTED_CERT" | sed 's/fullchain.pem/chain.pem/')
            sed -i "s|ssl_trusted_certificate.*|ssl_trusted_certificate $TRUSTED_CERT;|g" "$CONFIG_FILE"
        fi
        
        log "   ✓ Путь к сертификату исправлен"
    fi
done

echo ""

# 3. Проверить синтаксис nginx
log "3. Проверка синтаксиса nginx..."
if nginx -t 2>&1 | grep -q "successful"; then
    log "   ✓ Синтаксис nginx корректен"
else
    error "   ✗ Ошибка в синтаксисе nginx:"
    nginx -t
    exit 1
fi

# 4. Перезагрузить nginx
log "4. Перезагрузка nginx..."
systemctl reload nginx || error "Не удалось перезагрузить nginx"
log "   ✓ Nginx перезагружен"

echo ""

# 5. Проверить SSL
log "5. Проверка SSL соединения..."
sleep 2

for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    log "   Проверка $domain..."
    
    CERT_CN=$(echo | timeout 5 openssl s_client -connect "$domain:443" -servername "$domain" 2>/dev/null | grep "subject=CN" | sed 's/.*CN=\([^,]*\).*/\1/' || echo "")
    
    if echo "$CERT_CN" | grep -q "$domain"; then
        log "   ✓ SSL работает корректно (CN: $CERT_CN)"
    elif echo "$CERT_CN" | grep -q "wbmoneyback.ru"; then
        warn "   ⚠️  Все еще используется сертификат от wbmoneyback.ru"
        warn "      Проверьте конфигурацию nginx вручную"
    else
        warn "   ⚠️  Неожиданный CN: $CERT_CN"
    fi
done

echo ""

log "=== Исправление завершено ==="

