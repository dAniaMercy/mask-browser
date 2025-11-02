# Docker Compose для MASK BROWSER

## Быстрый старт

1. Создайте файл `.env` в директории `infra/`:

```bash
cp .env.example .env
# Отредактируйте .env файл с вашими настройками
```

2. Запустите все сервисы:

```bash
docker-compose up -d --build
```

3. Проверьте статус:

```bash
docker-compose ps
```

## Проблемы с портами

### PostgreSQL
По умолчанию PostgreSQL использует порт **5435** (внутренний 5432), чтобы избежать конфликтов с существующими контейнерами.

Чтобы изменить порт, отредактируйте `docker-compose.yml`:
```yaml
postgres:
  ports:
    - "ВАШ_ПОРТ:5432"
```

### Nginx
Nginx отключен по умолчанию (профиль `nginx`), чтобы избежать конфликтов с существующим nginx на сервере.

Чтобы включить наш nginx:
```bash
docker-compose --profile nginx up -d nginx
```

По умолчанию наш nginx использует порты:
- **8080** вместо 80
- **8443** вместо 443

### Cloudflare Tunnel
Cloudflare Tunnel опционален и запускается только если установлен `CLOUDFLARE_TUNNEL_TOKEN`.

Чтобы запустить с Cloudflare Tunnel:
```bash
# 1. Установите CLOUDFLARE_TUNNEL_TOKEN в .env
echo "CLOUDFLARE_TUNNEL_TOKEN=your_token_here" >> .env

# 2. Запустите с профилем cloudflare
docker-compose --profile cloudflare up -d cf_tunnel
```

## Сервисы

### Основные сервисы
- `postgres` - PostgreSQL база данных (порт 5435)
- `redis` - Redis кэш (порт 6379)
- `kafka` - Apache Kafka (порт 9092)
- `rabbitmq` - RabbitMQ (порты 5672, 15672)
- `api` - ASP.NET Core API (порт 5050)
- `web` - Next.js веб-приложение (порт 5052)
- `agent` - Go микросервис для управления контейнерами

### Мониторинг
- `prometheus` - Метрики (порт 9090)
- `grafana` - Дашборды (порт 3000)
- `loki` - Логи (порт 3100)
- `promtail` - Сбор логов
- `alertmanager` - Управление алертами (порт 9093)

### Опциональные сервисы
- `cf_tunnel` - Cloudflare Tunnel (профиль: `cloudflare`)
- `nginx` - Nginx Load Balancer (профиль: `nginx`)

## Полезные команды

### Просмотр логов
```bash
# Все логи
docker-compose logs -f

# Конкретный сервис
docker-compose logs -f api
docker-compose logs -f web
```

### Перезапуск сервисов
```bash
docker-compose restart api
docker-compose restart web
```

### Остановка всех сервисов
```bash
docker-compose down
```

### Остановка с удалением volumes
```bash
docker-compose down -v
```

## Проверка работы

1. API Health Check:
```bash
curl http://localhost:5050/health
```

2. Веб-интерфейс:
```bash
curl http://localhost:5052
```

3. Prometheus:
```bash
curl http://localhost:9090/-/healthy
```

4. Grafana:
```bash
curl http://localhost:3000/api/health
```

## Troubleshooting

### Проблема: Порт уже занят

Если порт уже занят другим контейнером:
1. Найдите контейнер, использующий порт:
```bash
docker ps | grep ПОРТ
```

2. Измените порт в `docker-compose.yml`:
```yaml
services:
  postgres:
    ports:
      - "НОВЫЙ_ПОРТ:5432"
```

### Проблема: Cloudflare Tunnel не запускается

Если `CLOUDFLARE_TUNNEL_TOKEN` не установлен, туннель не запустится. Это нормально.

Чтобы запустить туннель:
1. Получите токен из Cloudflare Dashboard
2. Добавьте в `.env`:
```bash
CLOUDFLARE_TUNNEL_TOKEN=your_token_here
```
3. Запустите с профилем:
```bash
docker-compose --profile cloudflare up -d cf_tunnel
```

### Проблема: Nginx конфликтует с существующим

Nginx отключен по умолчанию. Если нужно использовать наш nginx:
1. Остановите существующий nginx или
2. Используйте другой конфиг nginx в `nginx.conf`
3. Запустите с профилем:
```bash
docker-compose --profile nginx up -d nginx
```

