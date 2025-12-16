#!/bin/bash

# Скрипт для исправления проблем с Docker контейнерами
# Исправляет: Kafka, Loki, Agent, и другие проблемы

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

# Проверка прав
if [ "$EUID" -ne 0 ]; then
    error "Пожалуйста, запустите скрипт с правами root или через sudo"
fi

INFRA_DIR="/opt/mask-browser/infra"
cd "$INFRA_DIR" || error "Директория $INFRA_DIR не найдена"

log "=== Исправление проблем с Docker контейнерами ==="

# 1. Исправление конфига Loki
log "1. Исправление конфига Loki..."
if [ -f "loki-config.yml" ]; then
    # Создаем резервную копию
    cp loki-config.yml loki-config.yml.backup
    
    # Удаляем устаревшие поля
    sed -i '/enforce_metric_name:/d' loki-config.yml
    sed -i '/max_look_back_period:/d' loki-config.yml
    
    log "   ✓ Конфиг Loki обновлен"
else
    warn "   Файл loki-config.yml не найден"
fi

# 2. Исправление Kafka в docker-compose.yml
log "2. Исправление конфигурации Kafka..."
if [ -f "docker-compose.yml" ]; then
    # Создаем резервную копию
    cp docker-compose.yml docker-compose.yml.backup
    
    # Проверяем, есть ли уже KAFKA_PROCESS_ROLES
    if ! grep -q "KAFKA_PROCESS_ROLES" docker-compose.yml; then
        # Добавляем KAFKA_PROCESS_ROLES после KAFKA_BROKER_ID
        sed -i '/KAFKA_BROKER_ID: 1/a\      KAFKA_PROCESS_ROLES: broker' docker-compose.yml
        log "   ✓ Добавлен KAFKA_PROCESS_ROLES в конфиг Kafka"
    else
        log "   ✓ KAFKA_PROCESS_ROLES уже присутствует"
    fi
    
    # Также можно использовать старую версию Kafka, если новая не работает
    # Раскомментируйте следующую строку, если нужно:
    # sed -i 's|image: confluentinc/cp-kafka:latest|image: confluentinc/cp-kafka:7.5.0|g' docker-compose.yml
else
    warn "   Файл docker-compose.yml не найден"
fi

# 3. Проверка и пересборка Agent
log "3. Проверка Agent..."
if [ -f "../agent/main.go" ]; then
    log "   Пересборка Agent..."
    cd ../agent || warn "   Директория agent не найдена"
    
    # Проверяем, есть ли Dockerfile
    if [ -f "../infra/Dockerfile.agent" ]; then
        log "   ✓ Dockerfile.agent найден"
    else
        warn "   Dockerfile.agent не найден"
    fi
    cd "$INFRA_DIR"
else
    warn "   Исходники Agent не найдены"
fi

# 4. Остановка проблемных контейнеров
log "4. Остановка проблемных контейнеров..."
docker-compose stop kafka loki agent 2>/dev/null || true
log "   ✓ Контейнеры остановлены"

# 5. Удаление проблемных контейнеров
log "5. Удаление проблемных контейнеров..."
docker-compose rm -f kafka loki agent 2>/dev/null || true
log "   ✓ Контейнеры удалены"

# 6. Перезапуск Kafka и Loki
log "6. Перезапуск Kafka и Loki..."
docker-compose up -d kafka loki || warn "   Не удалось запустить Kafka/Loki"

# 7. Ожидание запуска Kafka
log "7. Ожидание запуска Kafka..."
sleep 10
if docker-compose ps kafka | grep -q "Up"; then
    log "   ✓ Kafka запущен"
else
    warn "   ✗ Kafka не запустился, проверьте логи: docker-compose logs kafka"
fi

# 8. Ожидание запуска Loki
log "8. Ожидание запуска Loki..."
sleep 5
if docker-compose ps loki | grep -q "Up"; then
    log "   ✓ Loki запущен"
else
    warn "   ✗ Loki не запустился, проверьте логи: docker-compose logs loki"
fi

# 9. Пересборка и запуск Agent
log "9. Пересборка и запуск Agent..."
docker-compose build agent || warn "   Не удалось собрать Agent"
docker-compose up -d agent || warn "   Не удалось запустить Agent"

# 10. Перезапуск основных сервисов
log "10. Перезапуск основных сервисов..."
docker-compose up -d api web maskadmin || warn "   Не удалось запустить некоторые сервисы"

# 11. Применение миграций БД
log "11. Применение миграций БД..."
docker-compose run --rm maskadmin dotnet ef database update || warn "   Не удалось применить миграции"

# 12. Проверка статуса
log "12. Проверка статуса контейнеров..."
sleep 5
docker-compose ps

log ""
log "=== Исправление завершено ==="
log ""
log "Проверьте статус контейнеров:"
log "  docker-compose ps"
log ""
log "Проверьте логи проблемных контейнеров:"
log "  docker-compose logs kafka"
log "  docker-compose logs loki"
log "  docker-compose logs agent"
log ""
log "Если проблемы сохраняются:"
log "  1. Проверьте логи: docker-compose logs [service]"
log "  2. Для Kafka можно использовать старую версию: confluentinc/cp-kafka:7.5.0"
log "  3. Для Loki проверьте версию образа и конфиг"
