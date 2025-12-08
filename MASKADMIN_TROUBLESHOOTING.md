# MaskAdmin Troubleshooting Guide

## Проблема: MaskAdmin не доступен на порту 5100

### Проверка статуса контейнера

```bash
# Проверить статус контейнера
docker ps -a | grep maskadmin

# Проверить логи контейнера
docker logs maskbrowser-maskadmin

# Проверить последние логи
docker logs --tail 100 maskbrowser-maskadmin
```

### Проверка портов

```bash
# Проверить, слушает ли контейнер порт
docker port maskbrowser-maskadmin

# Проверить, занят ли порт 5100 на хосте
netstat -tuln | grep 5100
# или
ss -tuln | grep 5100
```

### Проверка сети Docker

```bash
# Проверить сеть
docker network inspect infra_maskbrowser-network

# Проверить подключение к другим сервисам из контейнера
docker exec maskbrowser-maskadmin ping -c 3 postgres
docker exec maskbrowser-maskadmin ping -c 3 redis
docker exec maskbrowser-maskadmin ping -c 3 api
```

### Проверка health check

```bash
# Проверить health check вручную
docker exec maskbrowser-maskadmin wget -q -O- http://localhost:80/health

# Или через curl (если установлен)
docker exec maskbrowser-maskadmin curl -f http://localhost:80/health
```

### Перезапуск сервиса

```bash
# Остановить и удалить контейнер
docker-compose -f infra/docker-compose.yml stop maskadmin
docker-compose -f infra/docker-compose.yml rm -f maskadmin

# Пересобрать и запустить
docker-compose -f infra/docker-compose.yml build --no-cache maskadmin
docker-compose -f infra/docker-compose.yml up -d maskadmin

# Или перезапустить все сервисы
docker-compose -f infra/docker-compose.yml restart
```

### Проверка подключения к базе данных

```bash
# Проверить подключение к PostgreSQL из контейнера
docker exec maskbrowser-maskadmin dotnet exec /app/MaskAdmin.dll --check-db

# Или проверить через psql
docker exec -it maskbrowser-postgres psql -U maskuser -d maskbrowser -c "SELECT 1;"
```

### Типичные проблемы и решения

#### 1. Контейнер не запускается

**Причина:** Ошибки при старте приложения
**Решение:** 
- Проверить логи: `docker logs maskbrowser-maskadmin`
- Проверить подключение к БД и Redis
- Убедиться, что миграции применены

#### 2. Порт 5100 занят другим процессом

**Причина:** Другой сервис использует порт 5100
**Решение:**
```bash
# Найти процесс, использующий порт
lsof -i :5100
# или
fuser 5100/tcp

# Остановить процесс или изменить порт в docker-compose.yml
```

#### 3. Health check не проходит

**Причина:** Приложение не отвечает на /health endpoint
**Решение:**
- Увеличить `start_period` в health check
- Проверить, что endpoint `/health` доступен
- Проверить логи приложения

#### 4. Ошибки подключения к базе данных

**Причина:** Неправильная строка подключения или БД не готова
**Решение:**
- Проверить переменные окружения в docker-compose.yml
- Убедиться, что PostgreSQL запущен и здоров
- Проверить сеть Docker

#### 5. Ошибки подключения к Redis

**Причина:** Redis недоступен или неправильная конфигурация
**Решение:**
- Проверить, что Redis запущен: `docker ps | grep redis`
- Проверить строку подключения в переменных окружения
- Проверить логи Redis: `docker logs maskbrowser-redis`

### Проверка конфигурации

```bash
# Проверить переменные окружения контейнера
docker exec maskbrowser-maskadmin env | grep -E "ASPNETCORE|ConnectionStrings|JwtSettings"

# Проверить конфигурацию docker-compose
docker-compose -f infra/docker-compose.yml config | grep -A 20 maskadmin
```

### Доступ к приложению

После успешного запуска MaskAdmin должен быть доступен по адресу:
- **HTTP:** http://localhost:5100
- **Или:** http://<server-ip>:5100

### Проверка работоспособности

```bash
# Проверить доступность через curl
curl http://localhost:5100/health

# Проверить главную страницу
curl http://localhost:5100/

# Проверить метрики Prometheus
curl http://localhost:5100/metrics
```

### Логи для диагностики

```bash
# Последние 100 строк логов
docker logs --tail 100 maskbrowser-maskadmin

# Логи в реальном времени
docker logs -f maskbrowser-maskadmin

# Логи из файла (если настроено)
tail -f infra/logs/maskadmin/*.log
```
