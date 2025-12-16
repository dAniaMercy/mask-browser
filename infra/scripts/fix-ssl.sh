#!/bin/bash

# Скрипт для диагностики и исправления SSL сертификатов

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

log "=== Диагностика и исправление SSL сертификатов ==="
echo ""

# 1. Проверка наличия сертификатов
log "1. Проверка наличия SSL сертификатов..."

DOMAINS=("maskbrowser.ru" "admin.maskbrowser.ru")
CERT_MISSING=()

for domain in "${DOMAINS[@]}"; do
    CERT_PATH="/etc/letsencrypt/live/$domain"
    if [ -f "$CERT_PATH/fullchain.pem" ] && [ -f "$CERT_PATH/privkey.pem" ]; then
        log "   ✓ Сертификат для $domain найден"
        
        # Проверка срока действия
        EXPIRY=$(openssl x509 -enddate -noout -in "$CERT_PATH/fullchain.pem" | cut -d= -f2)
        EXPIRY_EPOCH=$(date -d "$EXPIRY" +%s 2>/dev/null || date -j -f "%b %d %H:%M:%S %Y %Z" "$EXPIRY" +%s 2>/dev/null || echo "0")
        NOW_EPOCH=$(date +%s)
        DAYS_LEFT=$(( ($EXPIRY_EPOCH - $NOW_EPOCH) / 86400 ))
        
        if [ $DAYS_LEFT -lt 30 ]; then
            warn "   ⚠️  Сертификат для $domain истекает через $DAYS_LEFT дней"
        else
            log "   ✓ Сертификат для $domain действителен еще $DAYS_LEFT дней"
        fi
    else
        warn "   ✗ Сертификат для $domain НЕ найден"
        CERT_MISSING+=("$domain")
    fi
done

echo ""

# 2. Проверка DNS
log "2. Проверка DNS записей..."
SERVER_IP="109.172.101.73"

for domain in "${DOMAINS[@]}"; do
    RESOLVED_IP=$(host "$domain" | grep "has address" | awk '{print $4}' | head -1)
    if [ "$RESOLVED_IP" = "$SERVER_IP" ]; then
        log "   ✓ $domain → $RESOLVED_IP"
    else
        warn "   ✗ $domain → $RESOLVED_IP (ожидается $SERVER_IP)"
    fi
done

echo ""

# 3. Проверка конфигурации nginx
log "3. Проверка конфигурации nginx..."

if nginx -t 2>&1 | grep -q "successful"; then
    log "   ✓ Синтаксис nginx корректен"
else
    error "   ✗ Ошибка в конфигурации nginx"
    nginx -t
fi

echo ""

# 4. Создание/обновление сертификатов
if [ ${#CERT_MISSING[@]} -gt 0 ]; then
    log "4. Создание отсутствующих SSL сертификатов..."
    
    # Убедимся, что директория для webroot существует
    mkdir -p /var/www/certbot
    
    for domain in "${CERT_MISSING[@]}"; do
        log "   Создание сертификата для $domain..."
        
        # Определяем домены для сертификата
        if [ "$domain" = "maskbrowser.ru" ]; then
            CERT_DOMAINS="-d maskbrowser.ru -d www.maskbrowser.ru"
        else
            CERT_DOMAINS="-d $domain"
        fi
        
        certbot certonly \
            --webroot \
            --webroot-path=/var/www/certbot \
            --email admin@maskbrowser.ru \
            --agree-tos \
            --no-eff-email \
            --non-interactive \
            $CERT_DOMAINS || warn "   ✗ Не удалось создать сертификат для $domain"
    done
else
    log "4. Все сертификаты присутствуют, проверка необходимости обновления..."
    
    # Проверяем, нужно ли обновить сертификаты
    for domain in "${DOMAINS[@]}"; do
        CERT_PATH="/etc/letsencrypt/live/$domain"
        EXPIRY=$(openssl x509 -enddate -noout -in "$CERT_PATH/fullchain.pem" | cut -d= -f2)
        EXPIRY_EPOCH=$(date -d "$EXPIRY" +%s 2>/dev/null || date -j -f "%b %d %H:%M:%S %Y %Z" "$EXPIRY" +%s 2>/dev/null || echo "0")
        NOW_EPOCH=$(date +%s)
        DAYS_LEFT=$(( ($EXPIRY_EPOCH - $NOW_EPOCH) / 86400 ))
        
        if [ $DAYS_LEFT -lt 30 ]; then
            log "   Обновление сертификата для $domain..."
            
            if [ "$domain" = "maskbrowser.ru" ]; then
                CERT_DOMAINS="-d maskbrowser.ru -d www.maskbrowser.ru"
            else
                CERT_DOMAINS="-d $domain"
            fi
            
            certbot certonly \
                --webroot \
                --webroot-path=/var/www/certbot \
                --email admin@maskbrowser.ru \
                --agree-tos \
                --no-eff-email \
                --force-renewal \
                --non-interactive \
                $CERT_DOMAINS || warn "   ✗ Не удалось обновить сертификат для $domain"
        fi
    done
fi

echo ""

# 5. Перезагрузка nginx
log "5. Перезагрузка nginx..."
systemctl reload nginx || error "Не удалось перезагрузить nginx"
log "   ✓ Nginx перезагружен"

echo ""

# 6. Проверка SSL соединения
log "6. Проверка SSL соединения..."

for domain in "${DOMAINS[@]}"; do
    log "   Проверка $domain..."
    
    # Проверка через openssl
    SSL_CHECK=$(echo | openssl s_client -connect "$domain:443" -servername "$domain" 2>/dev/null | grep -A2 "Verify return code" || echo "")
    
    if echo "$SSL_CHECK" | grep -q "Verify return code: 0"; then
        log "   ✓ SSL для $domain работает корректно"
    else
        warn "   ✗ Проблема с SSL для $domain"
        info "   Проверьте вручную: openssl s_client -connect $domain:443 -servername $domain"
    fi
done

echo ""

# 7. Проверка через curl (без проверки сертификата для диагностики)
log "7. Проверка доступности сайтов..."

for domain in "${DOMAINS[@]}"; do
    HTTP_CODE=$(curl -k -s -o /dev/null -w "%{http_code}" "https://$domain" || echo "000")
    
    if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "301" ] || [ "$HTTP_CODE" = "302" ]; then
        log "   ✓ $domain доступен (HTTP $HTTP_CODE)"
    else
        warn "   ✗ $domain недоступен (HTTP $HTTP_CODE)"
    fi
done

echo ""

log "=== Диагностика завершена ==="
echo ""
log "Полезные команды:"
log "  Проверить сертификаты: sudo certbot certificates"
log "  Обновить все сертификаты: sudo certbot renew"
log "  Проверить SSL: openssl s_client -connect maskbrowser.ru:443 -servername maskbrowser.ru"
log "  Проверить без SSL: curl -k -I https://maskbrowser.ru"
log ""
log "Если проблемы сохраняются:"
log "  1. Убедитесь, что DNS записи указывают на $SERVER_IP"
log "  2. Проверьте, что порты 80 и 443 открыты в firewall"
log "  3. Проверьте логи nginx: sudo tail -f /var/log/nginx/error.log"
