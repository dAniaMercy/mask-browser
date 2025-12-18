#!/bin/bash

# Скрипт для создания SSL сертификатов для maskbrowser.ru и admin.maskbrowser.ru

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

info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

if [ "$EUID" -ne 0 ]; then
    error "Пожалуйста, запустите скрипт с правами root или через sudo"
fi

log "=== Создание SSL сертификатов ==="
echo ""

# 1. Проверка DNS
log "1. Проверка DNS записей..."
SERVER_IP="109.172.101.73"
DOMAINS=("maskbrowser.ru" "www.maskbrowser.ru" "admin.maskbrowser.ru")

DNS_OK=true
for domain in "${DOMAINS[@]}"; do
    RESOLVED_IP=$(host "$domain" 2>/dev/null | grep "has address" | awk '{print $4}' | head -1)
    if [ "$RESOLVED_IP" = "$SERVER_IP" ]; then
        log "   ✓ $domain → $RESOLVED_IP"
    else
        warn "   ✗ $domain → $RESOLVED_IP (ожидается $SERVER_IP)"
        DNS_OK=false
    fi
done

if [ "$DNS_OK" = false ]; then
    warn ""
    warn "DNS записи настроены неправильно!"
    warn "Убедитесь, что все домены указывают на $SERVER_IP"
    read -p "Продолжить создание сертификатов? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 0
    fi
fi

echo ""

# 2. Проверка/создание директории для webroot
log "2. Проверка директории для webroot..."
mkdir -p /var/www/certbot
log "   ✓ Директория /var/www/certbot готова"

# 3. Проверка nginx конфигурации
log "3. Проверка конфигурации nginx..."
if nginx -t 2>&1 | grep -q "successful"; then
    log "   ✓ Синтаксис nginx корректен"
else
    error "   ✗ Ошибка в конфигурации nginx"
    nginx -t
fi

echo ""

# 4. Создание сертификата для maskbrowser.ru
log "4. Создание SSL сертификата для maskbrowser.ru..."

if [ -f "/etc/letsencrypt/live/maskbrowser.ru/fullchain.pem" ]; then
    warn "   Сертификат для maskbrowser.ru уже существует"
    read -p "   Обновить существующий сертификат? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        FORCE_RENEWAL="--force-renewal"
    else
        FORCE_RENEWAL=""
    fi
else
    FORCE_RENEWAL=""
fi

certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    --non-interactive \
    $FORCE_RENEWAL \
    -d maskbrowser.ru \
    -d www.maskbrowser.ru || error "Не удалось создать сертификат для maskbrowser.ru"

log "   ✓ Сертификат для maskbrowser.ru создан"

echo ""

# 5. Создание сертификата для admin.maskbrowser.ru
log "5. Создание SSL сертификата для admin.maskbrowser.ru..."

if [ -f "/etc/letsencrypt/live/admin.maskbrowser.ru/fullchain.pem" ]; then
    warn "   Сертификат для admin.maskbrowser.ru уже существует"
    read -p "   Обновить существующий сертификат? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        FORCE_RENEWAL="--force-renewal"
    else
        FORCE_RENEWAL=""
    fi
else
    FORCE_RENEWAL=""
fi

certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    --non-interactive \
    $FORCE_RENEWAL \
    -d admin.maskbrowser.ru || error "Не удалось создать сертификат для admin.maskbrowser.ru"

log "   ✓ Сертификат для admin.maskbrowser.ru создан"

echo ""

# 6. Проверка созданных сертификатов
log "6. Проверка созданных сертификатов..."
sudo certbot certificates

echo ""

# 7. Перезагрузка nginx
log "7. Перезагрузка nginx..."
systemctl reload nginx || error "Не удалось перезагрузить nginx"
log "   ✓ Nginx перезагружен"

echo ""

# 8. Проверка SSL
log "8. Проверка SSL соединения..."

for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    log "   Проверка $domain..."
    
    SSL_CHECK=$(echo | timeout 5 openssl s_client -connect "$domain:443" -servername "$domain" 2>/dev/null | grep -A2 "Verify return code" || echo "")
    
    if echo "$SSL_CHECK" | grep -q "Verify return code: 0"; then
        CERT_CN=$(echo | timeout 5 openssl s_client -connect "$domain:443" -servername "$domain" 2>/dev/null | grep "subject=CN" | sed 's/.*CN=\([^,]*\).*/\1/' || echo "")
        if echo "$CERT_CN" | grep -q "$domain"; then
            log "   ✓ SSL для $domain работает корректно (CN: $CERT_CN)"
        else
            warn "   ⚠️  SSL работает, но CN не совпадает: $CERT_CN"
        fi
    else
        warn "   ✗ Проблема с SSL для $domain"
    fi
done

echo ""

log "=== Создание сертификатов завершено ==="
echo ""
log "Проверьте работу сайтов:"
log "  curl -k -I https://maskbrowser.ru"
log "  curl -k -I https://admin.maskbrowser.ru"
log ""
log "Проверьте сертификаты:"
log "  sudo certbot certificates"
log ""
log "Настройте автообновление:"
log "  sudo certbot renew --dry-run"
