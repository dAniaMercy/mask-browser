# MaskAdmin Deployment Guide

## Развертывание на сервере

### Требования

- .NET 8.0 SDK
- PostgreSQL 14+
- Redis (опционально, для кэширования)
- Nginx (для reverse proxy)

### 1. Установка на сервере

```bash
cd /opt/mask-browser/MaskAdmin

# Восстановление зависимостей
dotnet restore

# Применение миграций базы данных
dotnet ef database update

# Сборка проекта
dotnet build -c Release

# Публикация приложения
dotnet publish -c Release -o /opt/mask-browser/MaskAdmin/publish
```

### 2. Конфигурация

Создайте файл `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=mask_browser;Username=postgres;Password=your_password"
  },
  "Jwt": {
    "Secret": "your-super-secret-key-min-32-characters-long!",
    "Issuer": "MaskAdmin",
    "Audience": "MaskAdminUsers",
    "ExpirationMinutes": 480
  },
  "Server": {
    "ApiBaseUrl": "http://server-api:8080"
  },
  "CryptoBot": {
    "WebhookSecret": "your-cryptobot-webhook-secret"
  },
  "Bybit": {
    "WebhookSecret": "your-bybit-webhook-secret"
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "Enabled": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 3. Systemd Service

Создайте файл `/etc/systemd/system/maskadmin.service`:

```ini
[Unit]
Description=MaskAdmin Web Application
After=network.target postgresql.service

[Service]
Type=notify
User=www-data
WorkingDirectory=/opt/mask-browser/MaskAdmin/publish
ExecStart=/usr/bin/dotnet /opt/mask-browser/MaskAdmin/publish/MaskAdmin.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=maskadmin
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Включите и запустите сервис:

```bash
sudo systemctl daemon-reload
sudo systemctl enable maskadmin
sudo systemctl start maskadmin
sudo systemctl status maskadmin
```

### 4. Nginx Reverse Proxy

Создайте конфигурацию Nginx `/etc/nginx/sites-available/maskadmin`:

```nginx
server {
    listen 80;
    server_name admin.yourdomain.com;

    # Redirect HTTP to HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name admin.yourdomain.com;

    # SSL certificates
    ssl_certificate /etc/letsencrypt/live/admin.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/admin.yourdomain.com/privkey.pem;

    # SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers on;
    ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # Webhook endpoints (no auth required)
    location /api/webhook/ {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Static files caching
    location ~* \.(css|js|jpg|jpeg|png|gif|ico|svg|woff|woff2|ttf|eot)$ {
        proxy_pass http://localhost:5000;
        expires 30d;
        add_header Cache-Control "public, immutable";
    }

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "no-referrer-when-downgrade" always;

    # Logging
    access_log /var/log/nginx/maskadmin_access.log;
    error_log /var/log/nginx/maskadmin_error.log;
}
```

Включите конфигурацию:

```bash
sudo ln -s /etc/nginx/sites-available/maskadmin /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### 5. SSL Certificate (Let's Encrypt)

```bash
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d admin.yourdomain.com
```

### 6. Firewall

```bash
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

## Развертывание через Docker

### 1. Docker Compose

Используйте `docker-compose.yml` из директории `infra`:

```bash
cd /opt/mask-browser/infra
docker-compose up -d maskadmin
```

### 2. Проверка логов

```bash
docker-compose logs -f maskadmin
```

### 3. Применение миграций в Docker

```bash
docker-compose exec maskadmin dotnet ef database update
```

## После развертывания

### 1. Создание администратора

При первом запуске администратор создается автоматически:
- Username: `admin`
- Password: `Admin123!`

Или создайте через API:

```bash
curl -X POST https://admin.yourdomain.com/create-admin \
  -H "Content-Type: application/json" \
  -d '{"password": "YourSecurePassword123!"}'
```

### 2. Проверка работоспособности

```bash
# Health check
curl https://admin.yourdomain.com/health

# Login test
curl -X POST https://admin.yourdomain.com/Auth/Login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "Admin123!"}'
```

### 3. Мониторинг

Prometheus метрики доступны по адресу:
```
https://admin.yourdomain.com/metrics
```

## Обновление приложения

```bash
# Остановить сервис
sudo systemctl stop maskadmin

# Обновить код
cd /opt/mask-browser/MaskAdmin
git pull

# Применить миграции
dotnet ef database update

# Пересобрать
dotnet publish -c Release -o /opt/mask-browser/MaskAdmin/publish

# Запустить сервис
sudo systemctl start maskadmin
```

## Troubleshooting

### Проблема: Приложение не запускается

**Проверьте логи:**
```bash
sudo journalctl -u maskadmin -n 100 --no-pager
```

**Проверьте порт:**
```bash
sudo netstat -tlnp | grep 5000
```

### Проблема: Ошибка подключения к БД

**Проверьте строку подключения в appsettings.Production.json**

**Проверьте, что PostgreSQL запущен:**
```bash
sudo systemctl status postgresql
```

**Проверьте доступ:**
```bash
psql -h localhost -U postgres -d mask_browser
```

### Проблема: 502 Bad Gateway

**Проверьте, что приложение запущено:**
```bash
sudo systemctl status maskadmin
```

**Проверьте логи Nginx:**
```bash
sudo tail -f /var/log/nginx/maskadmin_error.log
```

### Проблема: Rate Limiting блокирует IP

**Перезапустите приложение (in-memory хранилище очистится):**
```bash
sudo systemctl restart maskadmin
```

Или настройте Redis для распределенного хранилища:
- Установите Redis: `sudo apt install redis-server`
- Включите Redis в `appsettings.Production.json`: `"Redis": {"Enabled": true}`

## Безопасность

### 1. Смените пароль администратора

После первого входа обязательно смените пароль:
```bash
curl -X POST https://admin.yourdomain.com/reset-admin-password \
  -H "Content-Type: application/json" \
  -d '{"newPassword": "YourVerySecurePassword123!"}'
```

### 2. Настройте HTTPS

Используйте только HTTPS в production. Let's Encrypt предоставляет бесплатные сертификаты.

### 3. Ограничьте доступ

Используйте firewall для ограничения доступа к административной панели только с определенных IP.

### 4. Регулярные обновления

Регулярно обновляйте:
- .NET SDK
- NuGet пакеты
- PostgreSQL
- Nginx

### 5. Бэкапы

Настройте автоматические бэкапы базы данных:
```bash
# Ежедневный бэкап
0 2 * * * pg_dump -U postgres mask_browser > /backup/maskbrowser_$(date +\%Y\%m\%d).sql
```

## Производительность

### 1. Включите Redis для кэширования

В `appsettings.Production.json`:
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "Enabled": true
  }
}
```

### 2. Настройте Connection Pooling

PostgreSQL connection string:
```
Host=localhost;Port=5432;Database=mask_browser;Username=postgres;Password=pass;Pooling=true;MinPoolSize=5;MaxPoolSize=100
```

### 3. Включите Response Compression

Уже настроено в `Program.cs`.

### 4. Оптимизируйте Nginx

Увеличьте worker_connections в `/etc/nginx/nginx.conf`:
```nginx
events {
    worker_connections 4096;
}
```

## Мониторинг и логирование

### 1. Serilog

Логи пишутся в:
- Console (stdout)
- File: `/opt/mask-browser/MaskAdmin/logs/`
- PostgreSQL (таблица `AuditLogs`)

### 2. Prometheus

Метрики экспортируются на `/metrics`

### 3. Health Checks

Проверка здоровья: `/health`

Настройте мониторинг через cron:
```bash
*/5 * * * * curl -s https://admin.yourdomain.com/health > /dev/null || echo "MaskAdmin is down!" | mail -s "Alert" admin@yourdomain.com
```
