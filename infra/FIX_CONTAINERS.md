# Исправление проблем с Docker контейнерами

## Проблемы и решения

### 1. Kafka: "KAFKA_PROCESS_ROLES is not set"

**Проблема:** Новая версия Kafka требует переменную `KAFKA_PROCESS_ROLES`.

**Решение:**
```bash
# Добавить в docker-compose.yml в секцию kafka:
KAFKA_PROCESS_ROLES: broker
```

Или использовать старую версию Kafka:
```bash
# В docker-compose.yml изменить:
image: confluentinc/cp-kafka:7.5.0
```

### 2. Loki: ошибка парсинга конфига

**Проблема:** Устаревшие поля `enforce_metric_name` и `max_look_back_period` больше не поддерживаются.

**Решение:** Удалить эти поля из `infra/loki-config.yml` (уже исправлено).

### 3. Agent: "exec ./agent: no such file or directory"

**Проблема:** Бинарный файл не собран или не найден.

**Решение:**
```bash
cd /opt/mask-browser/infra
docker-compose build agent
docker-compose up -d agent
```

### 4. API/Web/MaskAdmin: Exit 0 (завершились)

**Проблема:** Контейнеры завершились вместо того, чтобы работать.

**Решение:**
```bash
cd /opt/mask-browser/infra
docker-compose up -d api web maskadmin
docker-compose logs api
docker-compose logs web
docker-compose logs maskadmin
```

### 5. Postgres: "database maskuser does not exist"

**Проблема:** Где-то используется неправильное имя БД в строке подключения.

**Решение:** Проверить все строки подключения - должно быть `Database=maskbrowser`, а не `Database=maskuser`.

### 6. Cryptobot: "TELEGRAM_API_ID and TELEGRAM_API_HASH must be set"

**Проблема:** Не заданы переменные окружения для Telegram API.

**Решение:** Либо задать переменные, либо остановить контейнер:
```bash
docker-compose stop cryptobot
# Или добавить в docker-compose.yml:
environment:
  - TELEGRAM_API_ID=${TELEGRAM_API_ID}
  - TELEGRAM_API_HASH=${TELEGRAM_API_HASH}
```

## Автоматическое исправление

Используйте скрипт для автоматического исправления:

```bash
cd /opt/mask-browser/infra
chmod +x scripts/fix-containers.sh
sudo bash scripts/fix-containers.sh
```

## Ручное исправление

### Шаг 1: Исправить конфиги

```bash
cd /opt/mask-browser/infra

# 1. Исправить Loki конфиг (уже исправлено в коде)
# Удалить строки с enforce_metric_name и max_look_back_period

# 2. Исправить docker-compose.yml для Kafka
# Добавить KAFKA_PROCESS_ROLES: broker после KAFKA_BROKER_ID
```

### Шаг 2: Остановить и пересобрать проблемные контейнеры

```bash
# Остановить проблемные контейнеры
docker-compose stop kafka loki agent api web maskadmin

# Удалить их
docker-compose rm -f kafka loki agent

# Пересобрать
docker-compose build agent
docker-compose build api web maskadmin

# Запустить
docker-compose up -d kafka loki
sleep 10
docker-compose up -d agent api web maskadmin
```

### Шаг 3: Применить миграции БД

```bash
docker-compose run --rm maskadmin dotnet ef database update
```

### Шаг 4: Проверить статус

```bash
docker-compose ps
docker-compose logs --tail=50 kafka
docker-compose logs --tail=50 loki
docker-compose logs --tail=50 agent
```

## Проверка работоспособности

```bash
# Проверить порты
curl -I http://localhost:5050/health  # API
curl -I http://localhost:5052           # Web
curl -I http://localhost:5100/health    # MaskAdmin

# Проверить Kafka
docker-compose exec kafka kafka-broker-api-versions --bootstrap-server localhost:9092

# Проверить Loki
curl http://localhost:3100/ready
```

## Если проблемы сохраняются

1. **Kafka не запускается:**
   - Используйте старую версию: `confluentinc/cp-kafka:7.5.0`
   - Или проверьте логи: `docker-compose logs kafka`

2. **Loki не запускается:**
   - Проверьте конфиг: `cat infra/loki-config.yml`
   - Проверьте логи: `docker-compose logs loki`

3. **Agent не запускается:**
   - Проверьте сборку: `docker-compose build agent`
   - Проверьте логи: `docker-compose logs agent`

4. **Сервисы завершаются:**
   - Проверьте логи: `docker-compose logs [service]`
   - Проверьте зависимости: `docker-compose ps`
   - Проверьте переменные окружения
