# MaskAdmin - Административная панель для MASK BROWSER

> Комплексная админская панель для управления системой антидетект браузеров MASK BROWSER

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-336791)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-7-DC382D)](https://redis.io/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

## 📋 Содержание

- [Возможности](#возможности)
- [Архитектура](#архитектура)
- [Установка](#установка)
- [Конфигурация](#конфигурация)
- [Использование](#использование)
- [API Документация](#api-документация)
- [Безопасность](#безопасность)

## 🚀 Возможности

### 📊 Dashboard (Главная панель)
- Общая статистика системы в реальном времени
- Графики активности пользователей и профилей
- Мониторинг состояния серверов
- Топ-5 пользователей по использованию
- Недавние события системы

### 👥 Управление пользователями
- **Просмотр**: список всех пользователей с фильтрацией и поиском
- **Редактирование**: изменение информации пользователя
- **Баланс**: добавление/списание средств с аккаунта
- **Блокировка**: бан/разбан пользователей
- **Заморозка**: временная приостановка аккаунта
- **Удаление**: полное удаление пользователя
- **Сброс пароля**: принудительная смена пароля
- **Статистика**: детальная статистика по каждому пользователю
- **Логи**: история всех действий пользователя

### 💎 Управление подписками
- Просмотр всех активных подписок
- Создание новых планов подписок (Free, Basic, Pro, Enterprise, Custom)
- Редактирование существующих планов
- Назначение подписок пользователям
- Автоматическое продление
- Статистика по подпискам и доходам

### 🖥️ Управление серверами
- Список всех серверных нод
- Мониторинг статуса (онлайн/оффлайн)
- Метрики в реальном времени:
  - CPU загрузка
  - RAM использование
  - Количество контейнеров
  - Network I/O
  - Disk usage
- Регистрация новых серверов
- Управление серверами (перезапуск, удаление)
- Просмотр контейнеров на каждом сервере
- Логи серверов

### 🌐 Управление профилями
- Список всех браузерных профилей
- Фильтрация по пользователю, статусу, серверу
- Запуск/остановка профилей
- Удаление профилей
- Просмотр конфигурации
- Статистика использования
- Логи профилей

### 💳 История платежей
- Список всех транзакций
- Фильтрация по статусу, провайдеру, пользователю
- Статистика доходов
- Экспорт в CSV/Excel
- Детальная информация по каждому платежу

### 📋 Системные логи
- **Категории логов**:
  - Authentication (входы, 2FA)
  - User Management (управление пользователями)
  - Profile Management (управление профилями)
  - Server Management (управление серверами)
  - Payment Management (платежи)
  - Security (безопасность)
  - System (системные события)
- Фильтрация по уровню (Debug, Info, Warning, Error, Critical)
- Поиск по тексту
- Экспорт логов
- Real-time обновление через WebSocket

### ⚙️ Настройки системы
- **General**: название системы, email администратора, timezone
- **Security**: JWT TTL, 2FA, rate limiting, IP whitelist/blacklist
- **Payment**: настройка платежных провайдеров, webhook
- **Server**: максимальное количество контейнеров, health check интервал
- **Email**: SMTP конфигурация, шаблоны писем

## 🏗️ Архитектура

```
MaskAdmin/
├── Controllers/          # MVC контроллеры
│   ├── DashboardController.cs
│   ├── UsersController.cs
│   ├── SubscriptionsController.cs
│   ├── ServersController.cs
│   ├── ProfilesController.cs
│   ├── PaymentsController.cs
│   ├── LogsController.cs
│   └── SettingsController.cs
├── Services/             # Бизнес-логика
│   ├── IServices.cs
│   ├── DashboardService.cs
│   ├── UserService.cs
│   ├── SubscriptionService.cs
│   ├── ServerService.cs
│   ├── ProfileService.cs
│   ├── PaymentService.cs
│   ├── LogService.cs
│   ├── SettingsService.cs
│   ├── ExportService.cs
│   └── NotificationService.cs
├── Models/               # Модели данных
│   ├── User.cs
│   ├── Subscription.cs
│   ├── BrowserProfile.cs
│   ├── ServerNode.cs
│   ├── Payment.cs
│   ├── AuditLog.cs
│   └── SystemSettings.cs
├── ViewModels/           # View Models для представлений
│   └── DashboardViewModel.cs
├── Views/                # Razor представления
│   ├── Dashboard/
│   ├── Users/
│   ├── Subscriptions/
│   ├── Servers/
│   ├── Profiles/
│   ├── Payments/
│   ├── Logs/
│   └── Settings/
├── Data/                 # Контекст базы данных
│   └── ApplicationDbContext.cs
├── wwwroot/              # Статические файлы
│   ├── css/
│   ├── js/
│   └── lib/
├── Dockerfile
├── docker-compose.yml
├── Program.cs
├── appsettings.json
└── MaskAdmin.csproj
```

### Технологический стек

**Backend:**
- ASP.NET Core 9.0 MVC
- Entity Framework Core 9.0
- PostgreSQL 15
- Redis 7 (кэширование)
- SignalR (real-time notifications)

**Frontend:**
- Razor Pages / MVC Views
- Bootstrap 5 / Tailwind CSS
- Chart.js / ApexCharts
- Alpine.js / jQuery

**Monitoring:**
- Prometheus (метрики)
- Grafana (визуализация)
- Serilog (логирование)

**Security:**
- JWT Authentication
- BCrypt (хеширование паролей)
- HTTPS/TLS
- Rate Limiting
- CORS

## 📦 Установка

### Предварительные требования

- .NET 8.0 SDK
- PostgreSQL 15+
- Redis 7+
- Docker & Docker Compose (опционально)

### Вариант 1: Docker Compose (рекомендуется)

```bash
# Клонировать репозиторий
git clone https://github.com/yourusername/maskadmin.git
cd maskadmin

# Запустить все сервисы
docker-compose up -d

# Проверить статус
docker-compose ps

# Приложение доступно на http://localhost:5100
```

### Вариант 2: Локальная установка

#### 1. Установка зависимостей

```bash
# Ubuntu/Debian
sudo apt update
sudo apt install -y postgresql redis-server

# macOS
brew install postgresql@15 redis

# Windows
# Установите PostgreSQL и Redis через установщики
```

#### 2. Настройка базы данных

```bash
# Подключиться к PostgreSQL
sudo -u postgres psql

# Создать базу данных и пользователя
CREATE DATABASE maskadmin;
CREATE USER maskadmin WITH PASSWORD 'maskadmin123';
GRANT ALL PRIVILEGES ON DATABASE maskadmin TO maskadmin;
\q
```

#### 3. Конфигурация

Отредактируйте `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=maskadmin;Username=maskadmin;Password=YOUR_PASSWORD",
    "Redis": "localhost:6379"
  },
  "JwtSettings": {
    "SecretKey": "CHANGE_THIS_TO_A_SECURE_SECRET_KEY_MIN_32_CHARACTERS"
  }
}
```

#### 4. Миграции базы данных

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

#### 5. Запуск приложения

```bash
dotnet restore
dotnet build
dotnet run
```

Приложение будет доступно на:
- HTTP: http://localhost:5100
- HTTPS: https://localhost:5101

## ⚙️ Конфигурация

### Переменные окружения

```bash
# Database
ConnectionStrings__PostgreSQL=Host=postgres;Port=5432;Database=maskadmin;Username=maskadmin;Password=maskadmin123
ConnectionStrings__Redis=redis:6379

# JWT
JwtSettings__SecretKey=your-super-secret-key-min-32-chars
JwtSettings__ExpirationMinutes=480

# API
MaskBrowserAPI__BaseUrl=http://localhost:5050

# Logging
Serilog__MinimumLevel__Default=Information
```

### Настройка Prometheus

Создайте `prometheus.yml`:

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'maskadmin'
    static_configs:
      - targets: ['maskadmin:80']
```

## 🎯 Использование

### Первый вход

**Учетные данные по умолчанию:**
- Email: `admin@maskbrowser.com`
- Password: `Admin123!`

⚠️ **Важно**: Измените пароль при первом входе!

### Основные операции

#### Создание пользователя

1. Перейдите в раздел **Users**
2. Нажмите **Add New User**
3. Заполните форму
4. Нажмите **Create**

#### Назначение подписки

1. Откройте пользователя
2. Нажмите **Manage Subscription**
3. Выберите план подписки
4. Укажите количество профилей и срок действия
5. Нажмите **Assign**

#### Регистрация сервера

1. Перейдите в **Servers**
2. Нажмите **Register New Server**
3. Укажите IP адрес и максимальное количество контейнеров
4. Нажмите **Register**

#### Экспорт данных

1. Перейдите в нужный раздел (Payments, Logs и т.д.)
2. Настройте фильтры
3. Нажмите **Export**
4. Выберите формат (CSV или Excel)

## 📚 API Документация

### Dashboard API

```http
GET /api/dashboard/stats
GET /api/dashboard/charts/profiles?days=7
GET /api/dashboard/charts/users?days=7
GET /api/dashboard/charts/revenue?days=30
```

### Users API

```http
GET /api/users?page=1&size=50&search=&status=&sort=
GET /api/users/{id}
PUT /api/users/{id}/edit
POST /api/users/{id}/balance/adjust
POST /api/users/{id}/ban
POST /api/users/{id}/unban
POST /api/users/{id}/freeze
DELETE /api/users/{id}
POST /api/users/{id}/reset-password
GET /api/users/{id}/stats
GET /api/users/{id}/logs
```

### Примеры запросов

#### Получение статистики

```bash
curl -X GET "http://localhost:5100/api/dashboard/stats" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

#### Изменение баланса пользователя

```bash
curl -X POST "http://localhost:5100/api/users/5/balance/adjust" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 100.00,
    "reason": "Bonus payment"
  }'
```

## 🔒 Безопасность

### Уровни доступа

1. **Super Admin** - полный доступ ко всем функциям
2. **Admin** - управление пользователями и подписками
3. **Moderator** - просмотр и базовые операции
4. **Support** - только чтение

### Audit Trail

Все действия администраторов логируются:
- Кто выполнил действие
- Когда (timestamp)
- Что изменено (старые и новые значения)
- IP адрес и User Agent
- Дополнительные данные

### Рекомендации по безопасности

1. **Измените JWT SecretKey** в production
2. **Используйте сильные пароли** для БД и Redis
3. **Включите HTTPS** в production
4. **Настройте Rate Limiting** для защиты от DDoS
5. **Регулярно обновляйте** зависимости
6. **Настройте Firewall** для ограничения доступа
7. **Включите 2FA** для всех администраторов
8. **Регулярно проверяйте** audit logs

## 📊 Мониторинг

### Prometheus Metrics

```
# Пользователи
maskadmin_users_total
maskadmin_users_active
maskadmin_users_banned

# Профили
maskadmin_profiles_total
maskadmin_profiles_active

# Серверы
maskadmin_servers_total
maskadmin_servers_healthy

# Платежи
maskadmin_payments_total
maskadmin_revenue_total
```

### Grafana Dashboards

Доступен по адресу: http://localhost:3001

**Дашборды:**
- System Overview
- User Activity
- Server Performance
- Revenue Analytics

## 🐛 Troubleshooting

### База данных не подключается

```bash
# Проверить статус PostgreSQL
sudo systemctl status postgresql

# Проверить подключение
psql -h localhost -U maskadmin -d maskadmin

# Проверить логи
tail -f /var/log/postgresql/postgresql-15-main.log
```

### Redis не работает

```bash
# Проверить статус
sudo systemctl status redis

# Проверить подключение
redis-cli ping

# Проверить логи
tail -f /var/log/redis/redis-server.log
```

### Приложение не запускается

```bash
# Проверить логи
tail -f logs/maskadmin-*.log

# Проверить порты
sudo netstat -tulpn | grep 5100

# Пересобрать приложение
dotnet clean
dotnet restore
dotnet build
dotnet run
```

## 🔄 Обновление

```bash
# Остановить приложение
docker-compose down

# Получить обновления
git pull

# Применить миграции
dotnet ef database update

# Перезапустить
docker-compose up -d
```

## 📝 Changelog

### Version 1.0.0 (2024-11-19)
- ✨ Первый релиз
- 📊 Dashboard с полной статистикой
- 👥 Управление пользователями
- 💎 Система подписок
- 🖥️ Мониторинг серверов
- 🌐 Управление профилями
- 💳 История платежей
- 📋 Системные логи
- ⚙️ Настройки системы
- 🔒 JWT Authentication
- 📈 Prometheus metrics
- 🐳 Docker поддержка

## 📄 Лицензия

MIT License - см. [LICENSE](LICENSE)

## 👨‍💻 Автор

MASK BROWSER Team

## 🤝 Поддержка

- Email: support@maskbrowser.com
- Telegram: @maskbrowser_support
- Issues: [GitHub Issues](https://github.com/yourusername/maskadmin/issues)

---

**Made with ❤️ for MASK BROWSER**
