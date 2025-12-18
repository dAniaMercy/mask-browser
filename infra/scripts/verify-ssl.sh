#!/bin/bash

# Скрипт для проверки SSL сертификатов после создания

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

log "=== Проверка SSL сертификатов ==="
echo ""

# 1. Проверить наличие сертификатов
log "1. Проверка наличия сертификатов..."
sudo certbot certificates

echo ""

# 2. Проверить статус nginx
log "2. Проверка статуса nginx..."
if systemctl is-active --quiet nginx; then
    log "   ✓ Nginx запущен"
else
    warn "   ✗ Nginx не запущен, запускаем..."
    systemctl start nginx
    sleep 2
    if systemctl is-active --quiet nginx; then
        log "   ✓ Nginx запущен"
    else
        error "   ✗ Не удалось запустить nginx"
        exit 1
    fi
fi

echo ""

# 3. Проверить SSL соединение
log "3. Проверка SSL соединения..."

for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    log "   Проверка $domain..."
    
    # Проверка через openssl
    SSL_CHECK=$(echo | timeout 5 openssl s_client -connect "$domain:443" -servername "$domain" 2>/dev/null | grep -A2 "Verify return code" || echo "")
    CERT_CN=$(echo | timeout 5 openssl s_client -connect "$domain:443" -servername "$domain" 2>/dev/null | grep "subject=CN" | sed 's/.*CN=\([^,]*\).*/\1/' || echo "")
    
    if echo "$SSL_CHECK" | grep -q "Verify return code: 0"; then
        if echo "$CERT_CN" | grep -q "$domain"; then
            log "   ✓ SSL работает корректно (CN: $CERT_CN)"
        else
            warn "   ⚠️  SSL работает, но CN не совпадает: $CERT_CN"
        fi
    else
        warn "   ⚠️  Проблема с SSL проверкой"
    fi
done

echo ""

# 4. Проверить доступность сайтов
log "4. Проверка доступности сайтов..."

for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://$domain" 2>/dev/null || echo "000")
    
    if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "301" ] || [ "$HTTP_CODE" = "302" ]; then
        log "   ✓ $domain доступен (HTTP $HTTP_CODE)"
    else
        warn "   ⚠️  $domain недоступен (HTTP $HTTP_CODE)"
    fi
done

echo ""

# 5. Проверить автообновление
log "5. Проверка автообновления сертификатов..."
if [ -f "/etc/cron.d/certbot" ] || systemctl list-timers | grep -q certbot; then
    log "   ✓ Автообновление настроено"
else
    warn "   ⚠️  Автообновление не найдено (certbot должен был настроить автоматически)"
fi

echo ""

log "=== Проверка завершена ==="
echo ""
log "Сертификаты созданы и должны работать!"
log ""
log "Проверьте в браузере:"
log "  https://maskbrowser.ru"
log "  https://admin.maskbrowser.ru"
log ""
log "Для будущих обновлений certbot будет использовать webroot режим"
log "(если исправите проблему с ACME challenge) или standalone режим"
