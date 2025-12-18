#!/bin/bash

# Скрипт для копирования структуры конфига wbmoneyback.ru в maskbrowser.ru и admin.maskbrowser.ru

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

log "=== Копирование структуры конфига wbmoneyback.ru ==="
echo ""

# 1. Показать конфиг wbmoneyback.ru
WBMONEYBACK_CONFIG="/etc/nginx/sites-available/wbmoneyback.ru"

if [ ! -f "$WBMONEYBACK_CONFIG" ]; then
    error "Файл $WBMONEYBACK_CONFIG не найден"
    exit 1
fi

log "1. Структура конфига wbmoneyback.ru:"
echo ""
cat "$WBMONEYBACK_CONFIG" | head -60
echo ""

# 2. Создать резервные копии
log "2. Создание резервных копий..."

CONFIG_FILES=(
    "/etc/nginx/sites-available/maskbrowser.ru.conf"
    "/etc/nginx/sites-available/admin.maskbrowser.ru.conf"
)

for config in "${CONFIG_FILES[@]}"; do
    if [ -f "$config" ]; then
        cp "$config" "$config.backup.$(date +%Y%m%d_%H%M%S)"
        log "   ✓ Резервная копия создана: $config"
    fi
done

echo ""

# 3. Показать текущие конфиги для сравнения
log "3. Текущие конфиги (первые 40 строк):"
echo ""
for config in "${CONFIG_FILES[@]}"; do
    if [ -f "$config" ]; then
        echo "=== $(basename $config) ==="
        head -40 "$config"
        echo ""
    fi
done

echo ""
log "=== Следующие шаги ==="
log ""
log "Теперь нужно вручную скопировать структуру из wbmoneyback.ru"
log "в maskbrowser.ru и admin.maskbrowser.ru, заменив:"
log "  - server_name на соответствующие домены"
log "  - пути к SSL сертификатам"
log "  - proxy_pass на правильные порты"
log ""
log "Или используйте команды ниже для автоматического создания"

