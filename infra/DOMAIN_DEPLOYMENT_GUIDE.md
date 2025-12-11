# MaskBrowser Domain Deployment Guide

## Домены

- **maskbrowser.ru** → Client Web (React)
- **admin.maskbrowser.ru** → Admin Panel (ASP.NET Core MVC)

## Server IP
```
109.172.101.73
```

## Шаг 1: Настройка DNS

### 1.1 Добавьте DNS записи у вашего регистратора

| Тип | Имя | Значение | TTL |
|-----|-----|----------|-----|
| A | @ | 109.172.101.73 | 3600 |
| A | www | 109.172.101.73 | 3600 |
| A | admin | 109.172.101.73 | 3600 |

**Где настроить:**
- Reg.ru → Домены → maskbrowser.ru → Управление DNS
- Nic.ru → Домены → maskbrowser.ru → DNS-серверы и зона
- Cloudflare → DNS → Add record

Подробная инструкция: [DNS_SETUP.md](DNS_SETUP.md)

### 1.2 Проверьте DNS (подождите 5-10 минут)

```bash
# На сервере или локально
host maskbrowser.ru
host www.maskbrowser.ru
host admin.maskbrowser.ru

# Все должны возвращать: 109.172.101.73
```

Онлайн проверка: https://www.whatsmydns.net/

---

## Шаг 2: Подготовка сервера

### 2.1 Подключитесь к серверу

```bash
ssh root@109.172.101.73
```

### 2.2 Обновите код из GitHub

```bash
cd /opt/mask-browser
git pull origin main
```

### 2.3 Скопируйте Nginx конфигурации

```bash
# Создайте директорию для конфигов если её нет
mkdir -p /opt/mask-browser/infra/nginx

# Скопируйте файлы на сервер (если еще не сделано через git pull)
# maskbrowser.ru.conf
# admin.maskbrowser.ru.conf
```

---

## Шаг 3: Автоматическая настройка (РЕКОМЕНДУЕТСЯ)

### 3.1 Запустите скрипт автоматической настройки

```bash
cd /opt/mask-browser/infra/scripts
chmod +x setup-domain.sh
sudo ./setup-domain.sh
```

**Скрипт выполнит:**
1. ✅ Установку certbot (если не установлен)
2. ✅ Копирование Nginx конфигов
3. ✅ Проверку DNS
4. ✅ Получение SSL сертификатов
5. ✅ Настройку автообновления сертификатов
6. ✅ Перезагрузку Nginx
7. ✅ Проверку доступности сайтов

### 3.2 Следуйте инструкциям скрипта

Скрипт попросит подтвердить, что DNS настроены правильно.

---

## Шаг 4: Ручная настройка (если нужно)

Если автоматический скрипт не подходит:

### 4.1 Установите certbot

```bash
sudo apt update
sudo apt install -y certbot python3-certbot-nginx
```

### 4.2 Создайте webroot для certbot

```bash
sudo mkdir -p /var/www/certbot
sudo chown -R www-data:www-data /var/www/certbot
```

### 4.3 Скопируйте Nginx конфигурации

```bash
sudo cp /opt/mask-browser/infra/nginx/maskbrowser.ru.conf /etc/nginx/sites-available/
sudo cp /opt/mask-browser/infra/nginx/admin.maskbrowser.ru.conf /etc/nginx/sites-available/

sudo ln -sf /etc/nginx/sites-available/maskbrowser.ru.conf /etc/nginx/sites-enabled/
sudo ln -sf /etc/nginx/sites-available/admin.maskbrowser.ru.conf /etc/nginx/sites-enabled/
```

### 4.4 Временно отключите SSL (для получения сертификатов)

```bash
# Закомментируйте HTTPS секции
sudo sed -i '/listen 443/,/^}/s/^/#/' /etc/nginx/sites-available/maskbrowser.ru.conf
sudo sed -i '/listen 443/,/^}/s/^/#/' /etc/nginx/sites-available/admin.maskbrowser.ru.conf
```

### 4.5 Проверьте и перезагрузите Nginx

```bash
sudo nginx -t
sudo systemctl reload nginx
```

### 4.6 Получите SSL сертификаты

```bash
# Для maskbrowser.ru (включая www)
sudo certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    -d maskbrowser.ru \
    -d www.maskbrowser.ru

# Для admin.maskbrowser.ru
sudo certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    -d admin.maskbrowser.ru
```

### 4.7 Включите SSL в Nginx

```bash
# Раскомментируйте HTTPS секции
sudo sed -i '/listen 443/,/^}/s/^#//' /etc/nginx/sites-available/maskbrowser.ru.conf
sudo sed -i '/listen 443/,/^}/s/^#//' /etc/nginx/sites-available/admin.maskbrowser.ru.conf

sudo nginx -t
sudo systemctl reload nginx
```

### 4.8 Настройте автообновление сертификатов

```bash
# Создайте hook для перезагрузки Nginx
sudo bash -c 'cat > /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh <<EOF
#!/bin/bash
systemctl reload nginx
EOF'

sudo chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh

# Тест автообновления
sudo certbot renew --dry-run
```

---

## Шаг 5: Проверка

### 5.1 Проверьте доступность сайтов

```bash
# Основной сайт
curl -I https://maskbrowser.ru

# Админка
curl -I https://admin.maskbrowser.ru
```

Должны вернуть HTTP 200 или 302/301.

### 5.2 Проверьте SSL

```bash
# Проверить сертификат
echo | openssl s_client -connect maskbrowser.ru:443 -servername maskbrowser.ru 2>/dev/null | grep -A2 "Verify return code"

echo | openssl s_client -connect admin.maskbrowser.ru:443 -servername admin.maskbrowser.ru 2>/dev/null | grep -A2 "Verify return code"
```

Должно быть: `Verify return code: 0 (ok)`

### 5.3 Проверьте в браузере

Откройте в браузере:
- https://maskbrowser.ru - должна открыться клиентская веб-панель
- https://admin.maskbrowser.ru - должна открыться админ панель

Проверьте, что:
- ✅ Нет ошибок SSL
- ✅ Иконка замочка зеленая/серая (безопасное соединение)
- ✅ HTTP → HTTPS редирект работает
- ✅ www → non-www редирект работает

---

## Шаг 6: Обновление конфигураций приложений

### 6.1 MaskAdmin

Файл `appsettings.Production.json` уже обновлен и содержит:

```json
{
  "AllowedHosts": "admin.maskbrowser.ru",
  "ConnectionStrings": {
    "PostgreSQL": "Host=maskbrowser-postgres;Port=5432;Database=maskbrowser;Username=maskuser;Password=maskpass123;Pooling=true;MinPoolSize=5;MaxPoolSize=100;"
  }
}
```

### 6.2 Перезапустите контейнеры

```bash
cd /opt/mask-browser/infra
docker-compose restart maskadmin
docker-compose restart web
```

### 6.3 Проверьте логи

```bash
# MaskAdmin логи
docker-compose logs -f maskadmin

# Client Web логи
docker-compose logs -f web

# Nginx логи
sudo tail -f /var/log/nginx/admin.maskbrowser.ru_access.log
sudo tail -f /var/log/nginx/maskbrowser.ru_access.log
```

---

## Шаг 7: Настройка webhook'ов

### 7.1 CryptoBot Webhook

В настройках CryptoBot укажите:
```
https://admin.maskbrowser.ru/api/webhook/cryptobot
```

### 7.2 Bybit Webhook

В настройках Bybit укажите:
```
https://admin.maskbrowser.ru/api/webhook/bybit
```

### 7.3 Обновите секреты

Отредактируйте `appsettings.Production.json` на сервере:

```bash
cd /opt/mask-browser/MaskAdmin
nano appsettings.Production.json
```

Замените:
```json
{
  "CryptoBot": {
    "WebhookSecret": "ваш-реальный-секрет-cryptobot"
  },
  "Bybit": {
    "WebhookSecret": "ваш-реальный-секрет-bybit"
  }
}
```

Перезапустите:
```bash
docker-compose restart maskadmin
```

---

## Troubleshooting

### Проблема: 502 Bad Gateway

**Причина:** Контейнеры не запущены или недоступны

**Решение:**
```bash
# Проверьте контейнеры
docker ps | grep maskbrowser

# Перезапустите
docker-compose restart maskadmin web
```

### Проблема: SSL сертификат не получен

**Причина:** DNS еще не распространились

**Решение:**
```bash
# Проверьте DNS
host admin.maskbrowser.ru

# Если не резолвится, подождите еще
# Попробуйте снова через 30 минут
sudo certbot certonly --webroot ...
```

### Проблема: "Connection refused"

**Причина:** Firewall блокирует порты

**Решение:**
```bash
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw status
```

### Проблема: Админка показывает ошибку подключения к БД

**Причина:** Неправильное имя хоста PostgreSQL в production

**Решение:**
```bash
# Проверьте docker-compose.yml
# PostgreSQL должен называться: maskbrowser-postgres

# Проверьте в appsettings.Production.json
# Host должен быть: maskbrowser-postgres (не localhost!)
```

### Проблема: CORS ошибки в браузере

**Решение:** Проверьте, что Client Web правильно настроен для API запросов через прокси Nginx.

---

## Мониторинг

### Проверка статуса сервисов

```bash
# Docker контейнеры
docker ps

# Nginx
sudo systemctl status nginx

# SSL сертификаты (срок действия)
sudo certbot certificates
```

### Логи

```bash
# Real-time мониторинг
sudo tail -f /var/log/nginx/admin.maskbrowser.ru_access.log
sudo tail -f /var/log/nginx/maskbrowser.ru_access.log

# Docker логи
docker-compose logs -f --tail=100 maskadmin
docker-compose logs -f --tail=100 web
```

### Метрики

Prometheus доступен на: http://109.172.101.73:9090
Grafana доступна на: http://109.172.101.73:3000

**Рекомендуется:** Настроить Nginx reverse proxy и для Grafana:
- grafana.maskbrowser.ru → http://localhost:3000

---

## Безопасность

### Рекомендации

1. **Смените пароли в production:**
   ```bash
   # PostgreSQL
   # Redis
   # Webhook секреты
   ```

2. **Настройте firewall:**
   ```bash
   sudo ufw allow 22/tcp   # SSH
   sudo ufw allow 80/tcp   # HTTP
   sudo ufw allow 443/tcp  # HTTPS
   sudo ufw enable
   ```

3. **Ограничьте доступ к внутренним портам:**
   Убедитесь, что порты 5050, 5052, 5100 недоступны извне (только через Nginx).

4. **Регулярно обновляйте сертификаты:**
   Certbot делает это автоматически, но проверяйте:
   ```bash
   sudo certbot renew --dry-run
   ```

5. **Мониторьте логи на подозрительную активность:**
   ```bash
   sudo grep -i "error\|fail\|attack" /var/log/nginx/*.log
   ```

---

## Готово! 🎉

Ваши сайты теперь доступны по адресам:
- 🌐 **https://maskbrowser.ru** - основной сайт
- 🔐 **https://admin.maskbrowser.ru** - админ панель

### Следующие шаги:

1. ✅ Создайте администратора (если еще не сделано)
2. ✅ Настройте webhook'и для платежных систем
3. ✅ Протестируйте все функции
4. ✅ Настройте мониторинг и алерты
5. ✅ Создайте бэкапы базы данных

**Документация:**
- [DNS_SETUP.md](DNS_SETUP.md) - Настройка DNS
- [DEPLOYMENT.md](../MaskAdmin/DEPLOYMENT.md) - Развертывание MaskAdmin
- [AUTHENTICATION.md](../MaskAdmin/docs/AUTHENTICATION.md) - Система авторизации
