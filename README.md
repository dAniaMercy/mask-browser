# MASK BROWSER - AntiDetect System

MASK BROWSER — это высоконагруженная система антидетект браузеров с полной анонимностью и распределённой архитектурой. Каждый пользователь имеет свои браузерные профили, запускаемые в изолированных Docker-контейнерах.

## 🚀 Возможности

- **10 000+ одновременных профилей** — система выдерживает экстремальные нагрузки
- **50 000+ RPS** — API оптимизировано для высокой пропускной способности
- **Горизонтальное масштабирование** — автоматическое добавление нод при нагрузке
- **Полная изоляция** — каждый профиль работает в отдельном Docker контейнере
- **Мониторинг и аналитика** — Prometheus, Grafana, Loki для отслеживания системы
- **Балансировка нагрузки** — автоматическое распределение профилей по серверам

## 📋 Технологический стек

### Backend
- ASP.NET Core 8 Web API
- Entity Framework Core + Dapper
- PostgreSQL 16 (master-replica)
- Redis 7 (кэширование и сессии)
- **Kafka** (логи и аналитика контейнеров)
- **RabbitMQ** (мгновенные задачи: создание/удаление профилей)
- Docker SDK (управление контейнерами)
- Prometheus (метрики)
- **RSA256 JWT** (асимметричная подпись токенов)
- **2FA/TOTP** (двухфакторная аутентификация)

### Frontend
- **Next.js 14** + TypeScript
- TailwindCSS (темная/светлая тема)
- Framer Motion (анимации)
- Zustand (стейт-менеджмент)
- i18next (RU/EN интернационализация)

### Desktop
- C# WPF
- Chromium Embedded Framework (CEF)

### Infrastructure
- Docker Compose
- Nginx (балансировка нагрузки, Rate Limiting)
- **Cloudflare** (WAF, DDoS защита, Tunnel)
- Kafka + Zookeeper (очереди для аналитики)
- RabbitMQ (очереди для задач)
- Prometheus + Grafana + Alertmanager (мониторинг)
- Loki + Promtail (логи)
- Cybernetics API (автомасштабирование)
- **Agent Service** (Go микросервис для управления контейнерами)

## 🏗️ Структура проекта

```
MaskBrowser/
├── server/              # ASP.NET Core 8 API
│   ├── Controllers/
│   ├── Services/
│   ├── Models/
│   ├── Infrastructure/
│   └── BackgroundJobs/
├── client-web/          # React приложение
│   ├── src/
│   │   ├── components/
│   │   ├── pages/
│   │   ├── store/
│   │   └── i18n/
├── desktop/             # WPF приложение
│   ├── MainWindow.xaml
│   └── Services/
├── infra/               # Docker и конфигурации
│   ├── docker-compose.yml
│   ├── Dockerfile.server
│   ├── Dockerfile.browser
│   └── prometheus.yml
└── docs/                # Документация
    ├── architecture.md
    ├── scaling-guide.md
    └── api-reference.md
```

## 🚀 Быстрый старт

### Основной сервер: 109.172.101.73

Проект развёрнут на сервере `109.172.101.73` (Ubuntu 22.04 LTS). Этот сервер является центральной нодой и управляет всеми компонентами системы.

### Деплой на сервер 109.172.101.73

#### Шаг 1: Подключение к серверу

```bash
ssh root@109.172.101.73
apt update && apt upgrade -y
apt install -y git curl ufw docker.io docker-compose nginx net-tools
```

#### Шаг 2: Настройка портов и безопасности

```bash
ufw allow 22
ufw allow 80
ufw allow 443
ufw allow 5050
ufw allow 5052
ufw allow 9090
ufw allow 3000
ufw allow 9092  # Kafka
ufw allow 5672  # RabbitMQ
ufw enable
```

#### Шаг 3: Клонирование проекта и настройка окружения

```bash
cd /opt
git clone https://github.com/<your_repo>/mask-browser.git
cd mask-browser
touch .env
```

Создайте `.env` файл со следующим содержимым:
```env
POSTGRES_USER=maskadmin
POSTGRES_PASSWORD=SuperSecurePass!
POSTGRES_DB=maskbrowser
REDIS_PASSWORD=MaskRedis123
JWT_SECRET=superlongsecretjwtstring
SERVER_IP=109.172.101.73
KAFKA_BROKER=109.172.101.73:9092
RABBITMQ_HOST=109.172.101.73
RABBITMQ_USER=maskqueue
RABBITMQ_PASS=MaskQueue123
```

#### Шаг 4: Сборка контейнеров

```bash
cd infra
docker-compose up -d --build
docker ps
```

Это запустит:
- PostgreSQL (5432)
- Redis (6379)
- **Kafka + Zookeeper (9092, 2181)**
- RabbitMQ (5672, управление: 15672)
- Prometheus (9090)
- Grafana (3000)
- Loki (3100)
- Nginx (80)
- API Server (5050)

#### Шаг 5: Настройка Cloudflare и Nginx

**Автоматическая настройка:**
```bash
cd /opt/mask-browser/infra
chmod +x setup-cloudflare.sh
sudo ./setup-cloudflare.sh
```

**Или вручную:**
```bash
# Установка Cloudflared
wget https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb
dpkg -i cloudflared-linux-amd64.deb

# Настройка Nginx для Cloudflare
cp infra/nginx-cloudflare.conf /etc/nginx/nginx.conf
nginx -t
systemctl restart nginx
```

Подробнее см. [docs/cloudflare-setup.md](docs/cloudflare-setup.md)

#### Шаг 6: Проверка API и интерфейсов

```bash
curl http://109.172.101.73/api/health
# -> {"status":"OK"}

# Доступные интерфейсы:
# http://109.172.101.73         # Сайт
# http://109.172.101.73:3000     # Grafana (admin/admin)
# http://109.172.101.73:9090     # Prometheus
```

#### Шаг 7: Настройка Kafka и RabbitMQ

**Kafka:**
```bash
docker exec -it maskbrowser-kafka bash
kafka-topics.sh --create --topic profile-events --bootstrap-server localhost:9092
kafka-topics.sh --create --topic container-logs --bootstrap-server localhost:9092
```

**RabbitMQ:**
```bash
docker exec -it maskbrowser-rabbitmq bash
rabbitmqctl add_user maskqueue MaskQueue123
rabbitmqctl set_permissions -p / maskqueue ".*" ".*" ".*"
```

#### Шаг 8: Настройка базы данных

```bash
cd /opt/mask-browser/server
dotnet ef migrations add InitialCreate
dotnet ef database update

# После добавления 2FA полей выполните:
dotnet ef migrations add AddTwoFactorAuthentication
dotnet ef database update
```

#### Шаг 9: Генерация RSA ключей

RSA ключи для JWT генерируются автоматически при первом запуске API сервера и сохраняются в `./keys/`.

⚠️ **ВАЖНО**: Убедитесь, что директория `keys/` добавлена в `.gitignore`!

### Локальная разработка

Для локальной разработки используйте те же команды, но без указания IP:

```bash
cd infra
docker-compose up -d
cd ../server
dotnet run --project MaskBrowser.Server.csproj
# API доступно на http://localhost:5050

cd ../client-web
npm install
npm run dev
# Frontend доступен на http://localhost:5052
```

## 📖 Использование

### Создание профиля

1. Зарегистрируйтесь или войдите через веб-интерфейс
2. Нажмите "Создать профиль"
3. Укажите название и настройки профиля
4. Нажмите "Запустить" для запуска контейнера

### API примеры

**Регистрация:**
```bash
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "SecurePass123!"
  }'
```

**Создание профиля:**
```bash
curl -X POST http://localhost:5050/api/profile \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Profile",
    "config": {
      "userAgent": "Mozilla/5.0...",
      "screenResolution": "1920x1080",
      "timezone": "UTC",
      "language": "en-US"
    }
  }'
```

## 🔧 Конфигурация

### Переменные окружения

Создайте `.env` файл в корне проекта:

```env
POSTGRES_PASSWORD=maskpass123
REDIS_PASSWORD=
JWT_KEY=your-super-secret-jwt-key-change-in-production
RABBITMQ_USER=guest
RABBITMQ_PASS=guest
```

### appsettings.json

Настройте подключения в `server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=postgres;Port=5432;Database=maskbrowser;Username=maskuser;Password=maskpass123",
    "Redis": "redis:6379"
  },
  "Jwt": {
    "Key": "your-secret-key",
    "ExpirationMinutes": 15
  }
}
```

## 📊 Мониторинг

### Grafana

Доступен на `http://localhost:3000`
- Логин: `admin`
- Пароль: `admin`

### Prometheus

Метрики доступны на `http://localhost:9090`

### Prometheus метрики API

```bash
curl http://localhost:5050/metrics
```

## 🔒 Безопасность

- Пароли хешируются с помощью Argon2
- JWT токены с коротким TTL (15 минут)
- Шифрование данных (AES-256)
- Изоляция контейнеров
- Ограничение ресурсов контейнеров

## 📈 Масштабирование

### Добавление новых серверов (автомасштабирование)

Каждый новый сервер должен иметь Docker и Docker Compose. После установки он подключается через API главного узла на `109.172.101.73`:

```bash
curl -X POST http://109.172.101.73/api/servers/register \
  -H "Content-Type: application/json" \
  -d '{
    "ip": "NEW_SERVER_IP",
    "capacity": 1000,
    "role": "node"
  }'
```

`LoadBalancerService` автоматически начнёт распределять профили между серверами.

### Использование Cybernetics API для автомасштабирования

Система поддерживает автоматическое создание новых нод через API Cybernetics:

1. Настройте API ключ в `appsettings.json`:
```json
{
  "Cybernetics": {
    "ApiUrl": "https://api.cybernetics.com",
    "ApiKey": "your-api-key"
  }
}
```

2. При достижении 80% нагрузки автоматически создаются новые ноды
3. Новые ноды автоматически регистрируются в системе

### Горизонтальное масштабирование API

```bash
docker-compose up -d --scale api=3
```

### Текущая архитектура масштабирования

- **Kafka** используется для логов и аналитики контейнеров
- **RabbitMQ** используется для мгновенных задач (создание, удаление профилей)
- **Redis** — для кеша и хранения сессий
- Автоматическое перераспределение нагрузки при пиках

Подробнее см. [docs/scaling-guide.md](docs/scaling-guide.md)

## 📚 Документация

- [Архитектура](docs/architecture.md)
- [Масштабирование](docs/scaling-guide.md)
- [API Reference](docs/api-reference.md)

## 🤝 Разработка

### Backend

```bash
cd server
dotnet restore
dotnet build
dotnet run
```

### Frontend

```bash
cd client-web
npm install
npm run dev
```

### Тестирование

```bash
# Backend
cd server
dotnet test

# Frontend
cd client-web
npm test
```

## 📝 Лицензия

[Укажите лицензию]

## 🙏 Поддержка

Для вопросов и поддержки создайте issue в репозитории.

---

**MASK BROWSER** — Система антидетект браузеров с экстремальной производительностью.
