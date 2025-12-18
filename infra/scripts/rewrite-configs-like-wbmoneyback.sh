#!/bin/bash

# Скрипт для переписывания конфигов по образцу wbmoneyback.ru

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

WBMONEYBACK_CONFIG="/etc/nginx/sites-available/wbmoneyback.ru"

if [ ! -f "$WBMONEYBACK_CONFIG" ]; then
    error "Файл $WBMONEYBACK_CONFIG не найден"
    exit 1
fi

log "=== Переписывание конфигов по образцу wbmoneyback.ru ==="
echo ""

# 1. Показать структуру wbmoneyback.ru
log "1. Структура конфига wbmoneyback.ru:"
echo ""
cat "$WBMONEYBACK_CONFIG"
echo ""
echo ""

# 2. Создать резервные копии
log "2. Создание резервных копий..."

BACKUP_DIR="/etc/nginx/sites-available/backup-$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

cp /etc/nginx/sites-available/maskbrowser.ru.conf "$BACKUP_DIR/" 2>/dev/null || true
cp /etc/nginx/sites-available/admin.maskbrowser.ru.conf "$BACKUP_DIR/" 2>/dev/null || true

log "   ✓ Резервные копии созданы в: $BACKUP_DIR"

echo ""

# 3. Прочитать конфиг wbmoneyback.ru и создать новые конфиги
log "3. Создание новых конфигов на основе wbmoneyback.ru..."

# Создать конфиг для maskbrowser.ru
log "   Создание maskbrowser.ru.conf..."

cat > /etc/nginx/sites-available/maskbrowser.ru.conf <<'MASKBROWSER_EOF'
# MaskBrowser Main Site Configuration
# Domain: maskbrowser.ru
# IP: 109.172.101.73

# HTTP to HTTPS redirect
server {
    listen 80;
    listen [::]:80;
    server_name maskbrowser.ru www.maskbrowser.ru;

    # Let's Encrypt ACME challenge
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    # Redirect all other traffic to HTTPS
    location / {
        return 301 https://maskbrowser.ru$request_uri;
    }
}

# HTTPS configuration
server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name maskbrowser.ru www.maskbrowser.ru;

    # SSL certificates (Let's Encrypt)
    ssl_certificate /etc/letsencrypt/live/maskbrowser.ru/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/maskbrowser.ru/privkey.pem;

    # SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # Logs
    access_log /var/log/nginx/maskbrowser.ru_access.log;
    error_log /var/log/nginx/maskbrowser.ru_error.log;

    # Client body size
    client_max_body_size 50M;

    # Proxy to Client Web (React app on port 5052)
    location / {
        proxy_pass http://localhost:5052;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # API proxy
    location /api/ {
        proxy_pass http://localhost:5050/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
MASKBROWSER_EOF

log "   ✓ maskbrowser.ru.conf создан"

# Создать конфиг для admin.maskbrowser.ru
log "   Создание admin.maskbrowser.ru.conf..."

cat > /etc/nginx/sites-available/admin.maskbrowser.ru.conf <<'ADMIN_EOF'
# MaskBrowser Admin Panel Configuration
# Domain: admin.maskbrowser.ru
# IP: 109.172.101.73

# HTTP to HTTPS redirect
server {
    listen 80;
    listen [::]:80;
    server_name admin.maskbrowser.ru;

    # Let's Encrypt ACME challenge
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    # Redirect all other traffic to HTTPS
    location / {
        return 301 https://admin.maskbrowser.ru$request_uri;
    }
}

# HTTPS configuration
server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name admin.maskbrowser.ru;

    # SSL certificates (Let's Encrypt)
    ssl_certificate /etc/letsencrypt/live/admin.maskbrowser.ru/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/admin.maskbrowser.ru/privkey.pem;

    # SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # Logs
    access_log /var/log/nginx/admin.maskbrowser.ru_access.log;
    error_log /var/log/nginx/admin.maskbrowser.ru_error.log;

    # Client body size
    client_max_body_size 100M;

    # Proxy to MaskAdmin (ASP.NET Core app on port 5100)
    location / {
        proxy_pass http://localhost:5100;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;

        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    # API endpoints
    location /api/ {
        proxy_pass http://localhost:5100/api/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
ADMIN_EOF

log "   ✓ admin.maskbrowser.ru.conf создан"

echo ""

# 4. Проверить синтаксис
log "4. Проверка синтаксиса nginx..."
if nginx -t 2>&1 | grep -q "successful"; then
    log "   ✓ Синтаксис nginx корректен"
else
    error "   ✗ Ошибка в синтаксисе nginx:"
    nginx -t
    error "   Восстановление из резервных копий..."
    cp "$BACKUP_DIR/maskbrowser.ru.conf" /etc/nginx/sites-available/ 2>/dev/null || true
    cp "$BACKUP_DIR/admin.maskbrowser.ru.conf" /etc/nginx/sites-available/ 2>/dev/null || true
    exit 1
fi

# 5. Перезагрузить nginx
log "5. Перезагрузка nginx..."
systemctl reload nginx || error "Не удалось перезагрузить nginx"
log "   ✓ Nginx перезагружен"

echo ""

# 6. Проверить SSL
log "6. Проверка SSL соединения..."
sleep 2

for domain in "maskbrowser.ru" "admin.maskbrowser.ru"; do
    log "   Проверка $domain..."
    
    CERT_CN=$(echo | timeout 5 openssl s_client -connect "$domain:443" -servername "$domain" 2>/dev/null | grep "subject=CN" | sed 's/.*CN=\([^,]*\).*/\1/' || echo "")
    
    if echo "$CERT_CN" | grep -q "$domain"; then
        log "   ✓ Используется правильный сертификат (CN: $CERT_CN)"
    elif echo "$CERT_CN" | grep -q "wbmoneyback.ru"; then
        warn "   ⚠️  Все еще используется сертификат от wbmoneyback.ru"
    else
        warn "   ⚠️  CN: $CERT_CN"
    fi
done

echo ""
log "=== Конфиги переписаны ==="
log ""
log "Резервные копии сохранены в: $BACKUP_DIR"
log ""
log "Если что-то не работает, восстановите из резервных копий:"
log "  sudo cp $BACKUP_DIR/*.conf /etc/nginx/sites-available/"

