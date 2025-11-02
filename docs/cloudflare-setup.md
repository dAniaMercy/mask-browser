# Настройка Cloudflare для MASK BROWSER

## Обзор

Проект использует Cloudflare для защиты, балансировки нагрузки и безопасной публикации сервисов через Cloudflare Tunnel.

Cloudflare обеспечивает:
- **WAF (Web Application Firewall)** — защита от SQL-инъекций, XSS, DDoS
- **DDoS Protection** — автоматическая защита от атак
- **Bot Fight Mode** — блокировка ботов
- **Rate Limiting** — ограничение запросов
- **SSL/TLS** — автоматические сертификаты
- **Tunnel** — безопасная публикация без открытых портов

## Требования

1. Аккаунт Cloudflare (бесплатный план достаточен)
2. Домен, добавленный в Cloudflare (или поддомен)
3. SSH доступ к серверу 109.172.101.73
4. Cloudflare Tunnel создан через Zero Trust

## Шаг 1: Автоматическая установка

Выполните на сервере скрипт автоматической настройки:

```bash
cd /opt/mask-browser/infra
chmod +x setup-cloudflare.sh
sudo ./setup-cloudflare.sh
```

Скрипт установит:
- Cloudflared
- Настроит UFW для Cloudflare IPs
- Настроит Nginx
- Настроит Fail2Ban
- Создаст .env файл с переменными

## Шаг 2: Создание Cloudflare Tunnel

1. Войдите в [Cloudflare Dashboard](https://dash.cloudflare.com)
2. Перейдите в **Zero Trust** > **Networks** > **Tunnels**
3. Нажмите **Create a tunnel**
4. Выберите **Cloudflared**
5. Назовите туннель: `mask-browser-tunnel`
6. Скопируйте **Tunnel Token** (будет использован в docker-compose)

## Шаг 3: Настройка DNS записей

После создания туннеля настройте маршрутизацию:

1. В настройках туннеля нажмите **Configure**
2. Добавьте Public Hostnames:

```
Public Hostname:
  Subdomain: @ (или www)
  Domain: yourdomain.com
  Service: http://web:5052
```

```
Public Hostname:
  Subdomain: api
  Domain: yourdomain.com
  Service: http://api:8080
```

```
Public Hostname:
  Subdomain: metrics (опционально, для внутреннего использования)
  Domain: yourdomain.com
  Service: http://prometheus:9090
```

3. Для каждого hostname убедитесь, что включен **Cloudflare Proxy** (оранжевое облако)

## Шаг 4: Настройка WAF Rules

1. Перейдите в **Security** > **WAF** > **Custom Rules**
2. Создайте правила:

**Правило 1: Блокировка SQL-инъекций**
```javascript
(http.request.uri.query contains "union select") or 
(http.request.uri.query contains "drop table") or
(http.request.uri.query contains "insert into") or
(http.request.uri.query contains "delete from") or
(http.request.uri.query contains "update ") or
(http.request.uri.query contains "exec(")
```

**Правило 2: Блокировка XSS**
```javascript
(http.request.uri.query contains "<script>") or
(http.request.body contains "<script>") or
(http.request.uri.query contains "javascript:") or
(http.request.uri.query contains "onerror=") or
(http.request.uri.query contains "onload=")
```

**Правило 3: Rate Limiting API**
```javascript
(http.request.uri.path contains "/api/") and 
(rate(100, 60))
```
Action: **Block**

**Правило 4: Rate Limiting Auth**
```javascript
(http.request.uri.path contains "/api/auth/") and 
(rate(10, 60))
```
Action: **Block**

## Шаг 5: Настройка Bot Fight Mode

1. Перейдите в **Security** > **Bots**
2. Выберите режим: **Super Bot Fight Mode** (платный) или **Bot Fight Mode** (бесплатный)
3. Для бесплатного плана включите **Bot Fight Mode**
4. Настройте **Challenge Passage** для легитимных ботов

## Шаг 6: Настройка SSL/TLS

1. Перейдите в **SSL/TLS** > **Overview**
2. Установите режим: **Full (Strict)**
3. Перейдите в **SSL/TLS** > **Edge Certificates**
4. Включите **Always Use HTTPS**
5. Включите **Automatic HTTPS Rewrites**
6. Перейдите в **SSL/TLS** > **Client Certificates** (опционально для дополнительной безопасности)
7. Для HSTS перейдите в **SSL/TLS** > **Settings**
8. Включите **HSTS** (min-age: 31536000, includeSubDomains: true, preload: false)

## Шаг 7: Настройка переменных окружения

Обновите `.env` файл в `/opt/mask-browser/.env`:

```env
CLOUDFLARE_TUNNEL_TOKEN=your-tunnel-token-here
CLOUDFLARE_TUNNEL_ID=your-tunnel-id
CLOUDFLARE_DOMAIN=yourdomain.com
```

⚠️ **ВАЖНО**: Замените `your-tunnel-token-here` на реальный токен из Cloudflare Dashboard!

## Шаг 8: Запуск Cloudflare Tunnel

```bash
cd /opt/mask-browser/infra
docker-compose up -d cf_tunnel
```

Проверьте логи:
```bash
docker logs maskbrowser-cf-tunnel
```

## Шаг 9: Проверка работы

Проверьте, что туннель работает:

```bash
# Проверка главной страницы
curl https://yourdomain.com

# Проверка API
curl https://api.yourdomain.com/health
# Ожидаемый ответ: {"status":"healthy","timestamp":"..."}

# Проверка через Cloudflare
curl -H "CF-Connecting-IP: 1.2.3.4" https://api.yourdomain.com/health
```

## Шаг 10: Настройка Fail2Ban

Fail2Ban уже настроен через `setup-cloudflare.sh`. Для проверки:

```bash
systemctl status fail2ban
fail2ban-client status
```

## Мониторинг

### Cloudflare Dashboard

В Cloudflare Dashboard отслеживайте:

1. **Analytics** > **Security Events**
   - WAF блокировки
   - Rate Limiting срабатывания
   - Bot Fight Mode блокировки

2. **Analytics** > **Web Traffic**
   - Запросы через туннель
   - Bandwidth usage

3. **Zero Trust** > **Networks** > **Tunnels**
   - Статус туннеля
   - Connected locations

### Логи на сервере

```bash
# Логи Cloudflare Tunnel
docker logs maskbrowser-cf-tunnel -f

# Логи Nginx
tail -f /var/log/nginx/access.log
tail -f /var/log/nginx/error.log

# Fail2Ban логи
tail -f /var/log/fail2ban.log
```

## Troubleshooting

### Туннель не подключается

1. Проверьте токен в `.env`
2. Проверьте DNS записи в Cloudflare
3. Проверьте логи: `docker logs maskbrowser-cf-tunnel`

### WAF блокирует легитимные запросы

1. Создайте WAF Exception Rule
2. Добавьте ваш IP в whitelist
3. Настройте Challenge Passage для вашего IP

### Высокая нагрузка

1. Проверьте Rate Limiting настройки
2. Рассмотрите Cloudflare Workers для кэширования
3. Используйте Cloudflare Cache Rules

## Рекомендации

1. **Включите Always Use HTTPS** — обязательно
2. **Настройте Rate Limiting** — защита от злоупотреблений
3. **Используйте Bot Fight Mode** — блокировка ботов
4. **Регулярно проверяйте WAF правила** — обновляйте паттерны
5. **Мониторьте Security Events** — отслеживайте атаки

