# 🔧 Команды для исправления проблем с контейнерами

## ⚡ Быстрое исправление (одна команда)

```bash
cd /opt/mask-browser/infra && sudo bash scripts/fix-containers.sh
```

---

## 📋 Пошаговые команды

### 1. Исправить Kafka

```bash
cd /opt/mask-browser/infra
# Добавить KAFKA_PROCESS_ROLES в docker-compose.yml (уже исправлено)
docker-compose stop kafka
docker-compose rm -f kafka
docker-compose up -d kafka
```

### 2. Исправить Loki

```bash
cd /opt/mask-browser/infra
# Конфиг уже исправлен (удалены устаревшие поля)
docker-compose stop loki
docker-compose rm -f loki
docker-compose up -d loki
```

### 3. Пересобрать и запустить Agent

```bash
cd /opt/mask-browser/infra
docker-compose build agent
docker-compose up -d agent
```

### 4. Перезапустить основные сервисы

```bash
cd /opt/mask-browser/infra
docker-compose up -d api web maskadmin
```

### 5. Применить миграции БД

```bash
cd /opt/mask-browser/infra
docker-compose run --rm maskadmin dotnet ef database update
```

### 6. Проверить статус

```bash
docker-compose ps
```

---

## 🔍 Диагностика

### Проверить логи проблемных контейнеров

```bash
docker-compose logs --tail=100 kafka
docker-compose logs --tail=100 loki
docker-compose logs --tail=100 agent
docker-compose logs --tail=100 api
docker-compose logs --tail=100 web
docker-compose logs --tail=100 maskadmin
```

### Проверить работоспособность

```bash
# API
curl -I http://localhost:5050/health

# Web
curl -I http://localhost:5052

# MaskAdmin
curl -I http://localhost:5100/health

# Kafka
docker-compose exec kafka kafka-broker-api-versions --bootstrap-server localhost:9092

# Loki
curl http://localhost:3100/ready
```

---

## 🛠️ Исправление конкретных проблем

### Kafka: "KAFKA_PROCESS_ROLES is not set"

```bash
# Вариант 1: Использовать старую версию Kafka
cd /opt/mask-browser/infra
sed -i 's|image: confluentinc/cp-kafka:latest|image: confluentinc/cp-kafka:7.5.0|g' docker-compose.yml
docker-compose up -d kafka

# Вариант 2: KAFKA_PROCESS_ROLES уже добавлен в docker-compose.yml
docker-compose up -d kafka
```

### Loki: ошибка парсинга конфига

```bash
# Конфиг уже исправлен, просто перезапустите
cd /opt/mask-browser/infra
docker-compose restart loki
```

### Agent: "exec ./agent: no such file or directory"

```bash
cd /opt/mask-browser/infra
docker-compose build --no-cache agent
docker-compose up -d agent
```

### Сервисы завершаются (Exit 0)

```bash
# Проверить логи
docker-compose logs api
docker-compose logs web
docker-compose logs maskadmin

# Перезапустить
docker-compose up -d api web maskadmin
```

### Postgres: "database maskuser does not exist"

```bash
# Проверить строки подключения в docker-compose.yml
# Должно быть: Database=maskbrowser (не maskuser)
grep -r "Database=maskuser" /opt/mask-browser
```

### Cryptobot: "TELEGRAM_API_ID must be set"

```bash
# Остановить контейнер, если не используется
docker-compose stop cryptobot

# Или добавить переменные в docker-compose.yml
```

---

## 🔄 Полный перезапуск

```bash
cd /opt/mask-browser/infra

# Остановить все
docker-compose down

# Пересобрать проблемные сервисы
docker-compose build --no-cache agent api web maskadmin

# Запустить инфраструктуру
docker-compose up -d postgres redis rabbitmq zookeeper
sleep 10

# Запустить Kafka и Loki
docker-compose up -d kafka loki
sleep 10

# Запустить основные сервисы
docker-compose up -d api web maskadmin agent

# Применить миграции
docker-compose run --rm maskadmin dotnet ef database update

# Проверить статус
docker-compose ps
```

---

## 📝 Полезные команды

```bash
# Статус всех контейнеров
docker-compose ps

# Логи всех сервисов
docker-compose logs --tail=50

# Перезапуск конкретного сервиса
docker-compose restart [service]

# Пересборка без кэша
docker-compose build --no-cache [service]

# Очистка неиспользуемых образов
docker image prune -f

# Очистка всего (осторожно!)
docker system prune -a
```
