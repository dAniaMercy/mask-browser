# Руководство по развертыванию Mask Browser

## Обзор сервисов

Проект состоит из следующих сервисов:

1. **PostgreSQL** - основная база данных
2. **Redis** - кэширование и дедупликация
3. **Kafka** + **Zookeeper** - очереди сообщений
4. **RabbitMQ** - быстрые задачи
5. **API** (ASP.NET Core) - основной API сервер
6. **Web** (Next.js) - фронтенд приложение
7. **MaskAdmin** (ASP.NET Core) - админ-панель
8. **CryptoBot Listener** (Python) - обработка платежей через Telegram
9. **Agent** (Go) - управление браузерными контейнерами
10. **Prometheus** + **Grafana** + **Loki** - мониторинг и логирование

## Быстрый старт

### 1. Клонирование и подготовка

```bash
git clone <repository>
cd MaskBrowser_old
```

### 2. Настройка переменных окружения

Создайте файл `.env` в директории `infra/`:

```env
# Telegram для CryptoBot Listener
TELEGRAM_API_ID=your_api_id
TELEGRAM_API_HASH=your_api_hash
TELEGRAM_PHONE=+7xxxxxxxxxx

# Webhook секрет (должен совпадать в API и CryptoBot Listener)
WEBHOOK_SECRET=your_super_secret_key_here

# JWT секрет для MaskAdmin
JWT_SECRET_KEY=your-super-secret-jwt-key-change-in-production-min-32-chars

# Опционально: Cloudflare Tunnel
CLOUDFLARE_TUNNEL_TOKEN=optional_token
```

### 3. Запуск всех сервисов

```bash
cd infra
docker-compose up -d
```

### 4. Проверка статуса

```bash
docker-compose ps
```

Все сервисы должны быть в статусе `Up` и `healthy`.

## Порты сервисов

| Сервис | Порт | Описание |
|--------|------|----------|
| API | 5050 | Основной API |
| Web | 5052 | Фронтенд приложение |
| MaskAdmin | 5100 | Админ-панель |
| PostgreSQL | 5435 | База данных |
| Redis | 6379 | Кэш |
| RabbitMQ | 15672 | Management UI |
| Kafka | 9092 | Message broker |
| Prometheus | 9090 | Метрики |
| Grafana | 3000 | Дашборды |
| Loki | 3100 | Логи |

## Первый запуск

### 1. Применение миграций БД

Миграции применяются автоматически при запуске API и MaskAdmin.

Для ручного применения:

```bash
# API миграции
docker-compose exec api dotnet ef database update --project /app

# MaskAdmin миграции (если нужны)
docker-compose exec maskadmin dotnet ef database update --project /app
```

### 2. Настройка Telegram для CryptoBot Listener

При первом запуске CryptoBot Listener запросит код подтверждения:

```bash
docker-compose logs -f cryptobot-listener
```

Введите код из SMS, когда появится запрос.

### 3. Вход в MaskAdmin

По умолчанию создается администратор:
- **Username:** `admin`
- **Password:** `Admin123!`

**ВАЖНО:** Измените пароль после первого входа!

URL: http://localhost:5100

## Управление сервисами

### Запуск отдельных сервисов

```bash
# Только основные сервисы (без мониторинга)
docker-compose up -d postgres redis api web maskadmin

# С CryptoBot Listener
docker-compose up -d postgres redis api web maskadmin cryptobot-listener

# Все сервисы включая мониторинг
docker-compose up -d
```

### Остановка сервисов

```bash
# Остановить все
docker-compose down

# Остановить с удалением volumes (ОСТОРОЖНО!)
docker-compose down -v
```

### Перезапуск сервиса

```bash
docker-compose restart maskadmin
```

### Просмотр логов

```bash
# Все логи
docker-compose logs -f

# Конкретный сервис
docker-compose logs -f api
docker-compose logs -f cryptobot-listener
docker-compose logs -f maskadmin
```

## Обновление

### 1. Остановить сервисы

```bash
docker-compose down
```

### 2. Обновить код

```bash
git pull
```

### 3. Пересобрать образы

```bash
docker-compose build --no-cache
```

### 4. Запустить заново

```bash
docker-compose up -d
```

## Мониторинг

### Prometheus

Доступен по адресу: http://localhost:9090

### Grafana

Доступна по адресу: http://localhost:3000
- **Username:** `admin`
- **Password:** `admin`

### Логи через Loki

Логи доступны в Grafana через Loki datasource.

## Troubleshooting

### Сервис не запускается

1. Проверьте логи:
   ```bash
   docker-compose logs <service-name>
   ```

2. Проверьте переменные окружения:
   ```bash
   docker-compose exec <service-name> env
   ```

3. Проверьте зависимости:
   ```bash
   docker-compose ps
   ```

### Проблемы с БД

1. Проверьте подключение:
   ```bash
   docker-compose exec postgres psql -U maskuser -d maskbrowser
   ```

2. Проверьте миграции:
   ```bash
   docker-compose exec api dotnet ef migrations list --project /app
   ```

### Проблемы с сетью

Все сервисы должны быть в сети `maskbrowser-network`:

```bash
docker network inspect maskbrowser_old_maskbrowser-network
```

### Очистка и перезапуск

Если что-то пошло не так:

```bash
# Остановить все
docker-compose down

# Удалить volumes (ОСТОРОЖНО - удалит данные!)
docker-compose down -v

# Пересобрать
docker-compose build --no-cache

# Запустить заново
docker-compose up -d
```

## Production рекомендации

1. **Измените все пароли по умолчанию**
2. **Используйте секреты** вместо переменных окружения в `.env`
3. **Настройте SSL/TLS** для всех сервисов
4. **Настройте резервное копирование БД**
5. **Настройте мониторинг и алерты**
6. **Ограничьте доступ к портам** через firewall
7. **Используйте reverse proxy** (nginx/traefik) для маршрутизации

## Дополнительная документация

- [Настройка системы депозитов](./DEPOSIT_SETUP.md)
- [Архитектура системы](./docs/architecture.md)
- [API документация](./docs/api-reference.md)
- [Безопасность](./docs/security.md)
