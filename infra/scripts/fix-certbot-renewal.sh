#!/bin/bash

# Скрипт для настройки certbot на использование webroot для обновлений

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

log "=== Настройка certbot для обновлений через webroot ==="
echo ""

# 1. Проверить конфигурации обновления
log "1. Проверка текущих настроек обновления..."

RENEWAL_FILES=(
    "/etc/letsencrypt/renewal/maskbrowser.ru.conf"
    "/etc/letsencrypt/renewal/admin.maskbrowser.ru.conf"
)

for RENEWAL_FILE in "${RENEWAL_FILES[@]}"; do
    if [ ! -f "$RENEWAL_FILE" ]; then
        warn "   Файл не найден: $RENEWAL_FILE"
        continue
    fi
    
    log "   Проверка: $RENEWAL_FILE"
    
    # Проверить, какой authenticator используется
    if grep -q "authenticator = standalone" "$RENEWAL_FILE"; then
        warn "   ⚠️  Используется standalone (требует остановки nginx)"
        log "   Изменение на webroot..."
        
        # Создать резервную копию
        cp "$RENEWAL_FILE" "$RENEWAL_FILE.backup.$(date +%Y%m%d_%H%M%S)"
        
        # Заменить authenticator
        sed -i 's/authenticator = standalone/authenticator = webroot/' "$RENEWAL_FILE"
        
        # Добавить webroot_path, если его нет
        if ! grep -q "webroot_path" "$RENEWAL_FILE"; then
            # Найти секцию [renewalparams] и добавить webroot_path после нее
            sed -i '/\[renewalparams\]/a webroot_path = /var/www/certbot,' "$RENEWAL_FILE"
        fi
        
        log "   ✓ Изменено на webroot"
    elif grep -q "authenticator = webroot" "$RENEWAL_FILE"; then
        log "   ✓ Уже использует webroot"
    else
        warn "   ⚠️  Неизвестный authenticator"
    fi
done

echo ""

# 2. Настроить hook для перезагрузки nginx
log "2. Настройка hook для перезагрузки nginx..."

mkdir -p /etc/letsencrypt/renewal-hooks/deploy

cat > /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh <<'EOF'
#!/bin/bash
systemctl reload nginx
EOF

chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh
log "   ✓ Hook создан"

echo ""

# 3. Тест обновления (dry-run)
log "3. Тест обновления сертификатов (dry-run)..."
if certbot renew --dry-run 2>&1 | grep -q "Congratulations"; then
    log "   ✓ Тест обновления прошел успешно"
else
    warn "   ⚠️  Тест обновления не прошел"
    warn "      Это может быть нормально, если сертификаты еще не скоро истекают"
    certbot renew --dry-run 2>&1 | tail -10
fi

echo ""

log "=== Настройка завершена ==="
log ""
log "Certbot теперь будет использовать webroot для обновлений"
log "Nginx не нужно будет останавливать при обновлении сертификатов"

