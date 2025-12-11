# MaskBrowser Deployment - Финальные шаги

## Текущая ситуация

На сервере **109.172.101.73** уже работает:
- ✅ **wbmoneyback.ru** - существующий сайт с SSL
- ✅ Nginx настроен
- ✅ Let's Encrypt работает

Нужно добавить:
- 🆕 **maskbrowser.ru** - основной сайт
- 🆕 **admin.maskbrowser.ru** - админ панель

---

## Шаг 1: Настройка DNS (СДЕЛАЙТЕ ПЕРВЫМ!)

### 1.1 Войдите в панель регистратора домена maskbrowser.ru

Добавьте 3 DNS записи:

| Тип | Имя | Значение | TTL |
|-----|-----|----------|-----|
| A | @ | 109.172.101.73 | 3600 |
| A | www | 109.172.101.73 | 3600 |
| A | admin | 109.172.101.73 | 3600 |

### 1.2 Подождите 10-30 минут

DNS должны распространиться. Проверяйте:

```bash
host maskbrowser.ru
host www.maskbrowser.ru
host admin.maskbrowser.ru
```

Все должны возвращать **109.172.101.73**

---

## Шаг 2: Подключитесь к серверу

```bash
ssh root@109.172.101.73
```

---

## Шаг 3: Обновите код

```bash
cd /opt/mask-browser
git pull origin main
```

Это загрузит все конфиги, которые мы создали:
- `infra/nginx/maskbrowser.ru.conf`
- `infra/nginx/admin.maskbrowser.ru.conf`
- `infra/scripts/setup-domain-safe.sh`
- `MaskAdmin/appsettings.Production.json`

---

## Шаг 4: Проверьте существующую конфигурацию (опционально)

```bash
cd /opt/mask-browser/infra/scripts
chmod +x check-existing-sites.sh
./check-existing-sites.sh
```

Это покажет:
- Существующие сайты
- Настроенные домены
- Используемые порты
- SSL сертификаты

---

## Шаг 5: Запустите автоматическую настройку

### Вариант A: Безопасный скрипт (РЕКОМЕНДУЕТСЯ)

```bash
cd /opt/mask-browser/infra/scripts
chmod +x setup-domain-safe.sh
sudo ./setup-domain-safe.sh
```

**Этот скрипт:**
- Покажет существующие конфиги (wbmoneyback.ru)
- Создаст бэкап перед изменениями
- Спросит подтверждение перед каждым важным шагом
- Не будет конфликтовать с wbmoneyback.ru
- Получит SSL сертификаты для maskbrowser.ru
- Настроит автообновление сертификатов

### Вариант B: Ручная настройка (если скрипт не подходит)

См. раздел "Ручная настройка" ниже.

---

## Шаг 6: Проверьте результат

### 6.1 Проверьте все 3 сайта

```bash
# Существующий сайт (должен продолжать работать)
curl -I https://wbmoneyback.ru

# Новые сайты MaskBrowser
curl -I https://maskbrowser.ru
curl -I https://admin.maskbrowser.ru
```

Все должны вернуть HTTP 200 или 30X.

### 6.2 Откройте в браузере

- https://wbmoneyback.ru - старый сайт (должен работать как раньше)
- https://maskbrowser.ru - новый основной сайт
- https://admin.maskbrowser.ru - админ панель

### 6.3 Проверьте SSL

В браузере должен быть зеленый/серый замочек (безопасное соединение).

---

## Шаг 7: Перезапустите контейнеры MaskBrowser

```bash
cd /opt/mask-browser/infra
docker-compose restart maskadmin web
```

### Проверьте логи

```bash
# MaskAdmin
docker-compose logs -f maskadmin

# Client Web
docker-compose logs -f web

# Должно быть без ошибок
```

---

## Шаг 8: Создайте администратора (если еще не сделано)

```bash
cd /opt/mask-browser/MaskAdmin/scripts
chmod +x create-admin.sh
./create-admin.sh "Admin123!"
```

Или через API:
```bash
curl -X POST http://localhost:5100/create-admin \
  -H "Content-Type: application/json" \
  -d '{"password": "Admin123!"}'
```

---

## Шаг 9: Войдите в админку

Откройте: **https://admin.maskbrowser.ru/Auth/Login**

**Учетные данные:**
- Username: `admin`
- Password: `Admin123!` (или ваш пароль)

---

## Ручная настройка (если нужно)

### 1. Скопируйте конфиги

```bash
sudo cp /opt/mask-browser/infra/nginx/maskbrowser.ru.conf /etc/nginx/sites-available/
sudo cp /opt/mask-browser/infra/nginx/admin.maskbrowser.ru.conf /etc/nginx/sites-available/

sudo ln -sf /etc/nginx/sites-available/maskbrowser.ru.conf /etc/nginx/sites-enabled/
sudo ln -sf /etc/nginx/sites-available/admin.maskbrowser.ru.conf /etc/nginx/sites-enabled/
```

### 2. Временно отключите SSL

```bash
sudo sed -i '/listen 443/,/^}/s/^/#/' /etc/nginx/sites-available/maskbrowser.ru.conf
sudo sed -i '/listen 443/,/^}/s/^/#/' /etc/nginx/sites-available/admin.maskbrowser.ru.conf
```

### 3. Проверьте и перезагрузите Nginx

```bash
sudo nginx -t
sudo systemctl reload nginx
```

### 4. Получите SSL сертификаты

```bash
# Для maskbrowser.ru
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

### 5. Включите SSL

```bash
sudo sed -i '/listen 443/,/^}/s/^#//' /etc/nginx/sites-available/maskbrowser.ru.conf
sudo sed -i '/listen 443/,/^}/s/^#//' /etc/nginx/sites-available/admin.maskbrowser.ru.conf

sudo nginx -t
sudo systemctl reload nginx
```

---

## Важные моменты

### ✅ Сайты будут работать параллельно

Nginx маршрутизирует по `server_name`:
- Запрос к `wbmoneyback.ru` → конфиг wbmoneyback.ru
- Запрос к `maskbrowser.ru` → конфиг maskbrowser.ru
- Запрос к `admin.maskbrowser.ru` → конфиг admin.maskbrowser.ru

### ✅ SSL сертификаты независимы

Каждый домен имеет свой сертификат:
- `/etc/letsencrypt/live/wbmoneyback.ru/`
- `/etc/letsencrypt/live/maskbrowser.ru/`
- `/etc/letsencrypt/live/admin.maskbrowser.ru/`

Certbot автоматически обновляет **все** сертификаты.

### ✅ Нет конфликтов портов

Все сайты используют порты 80/443, но Nginx правильно маршрутизирует по доменам.

---

## Архитектура после развертывания

```
Internet
    ↓
109.172.101.73
    ↓
Nginx (80/443)
    ├── wbmoneyback.ru → (ваш существующий сайт)
    ├── maskbrowser.ru → localhost:5052 (Client Web)
    └── admin.maskbrowser.ru → localhost:5100 (MaskAdmin)
         ↓
Docker Containers
    ├── maskbrowser-web (port 5052)
    ├── maskbrowser-maskadmin (port 5100)
    ├── maskbrowser-api (port 5050)
    ├── maskbrowser-postgres (port 5435)
    └── maskbrowser-redis (port 6379)
```

---

## Мониторинг

### Логи Nginx

```bash
# Все сайты
sudo tail -f /var/log/nginx/access.log
sudo tail -f /var/log/nginx/error.log

# MaskBrowser отдельно
sudo tail -f /var/log/nginx/maskbrowser.ru_access.log
sudo tail -f /var/log/nginx/admin.maskbrowser.ru_access.log
```

### Логи Docker

```bash
docker-compose logs -f maskadmin
docker-compose logs -f web
docker-compose logs -f api
```

### Статус сервисов

```bash
# Nginx
sudo systemctl status nginx

# Docker контейнеры
docker ps | grep maskbrowser

# SSL сертификаты
sudo certbot certificates
```

---

## Troubleshooting

### Проблема: wbmoneyback.ru перестал работать

**Причина:** Ошибка в новых конфигах повлияла на Nginx.

**Решение:**
```bash
# Проверьте конфигурацию
sudo nginx -t

# Если ошибка, отключите новые конфиги
sudo rm /etc/nginx/sites-enabled/maskbrowser.ru.conf
sudo rm /etc/nginx/sites-enabled/admin.maskbrowser.ru.conf

# Перезагрузите
sudo systemctl reload nginx

# wbmoneyback.ru должен заработать
```

### Проблема: "conflicting server name"

**Решение:**
```bash
# Проверьте дубликаты
grep -r "server_name maskbrowser.ru" /etc/nginx/sites-enabled/

# Удалите дубликат
```

### Проблема: SSL не работает для maskbrowser.ru

**Решение:**
```bash
# Проверьте сертификаты
sudo certbot certificates

# Пересоздайте если нужно
sudo certbot certonly --force-renewal -d maskbrowser.ru -d www.maskbrowser.ru
```

### Проблема: Admin panel показывает 502

**Причина:** Контейнер не запущен.

**Решение:**
```bash
docker ps | grep maskadmin
docker-compose restart maskadmin
docker-compose logs maskadmin
```

---

## Checklist финального развертывания

- [ ] DNS настроены (A записи добавлены)
- [ ] DNS резолвятся (проверено через `host`)
- [ ] Код обновлен из GitHub (`git pull`)
- [ ] Скрипт `setup-domain-safe.sh` запущен
- [ ] SSL сертификаты получены
- [ ] https://maskbrowser.ru открывается
- [ ] https://admin.maskbrowser.ru открывается
- [ ] https://wbmoneyback.ru продолжает работать
- [ ] Контейнеры перезапущены
- [ ] Администратор создан
- [ ] Вход в админку работает
- [ ] Webhook URLs обновлены (CryptoBot, Bybit)

---

## Следующие шаги

После успешного развертывания:

1. **Настройте webhook'и** в платежных системах:
   - CryptoBot: `https://admin.maskbrowser.ru/api/webhook/cryptobot`
   - Bybit: `https://admin.maskbrowser.ru/api/webhook/bybit`

2. **Обновите секреты** в `appsettings.Production.json`:
   - `CryptoBot:WebhookSecret`
   - `Bybit:WebhookSecret`

3. **Протестируйте** все функции:
   - Регистрация пользователей
   - Создание профилей
   - Платежи
   - Управление через админку

4. **Настройте мониторинг**:
   - Проверяйте логи регулярно
   - Настройте alerts для ошибок

5. **Создайте бэкапы**:
   - База данных PostgreSQL
   - Конфигурационные файлы

---

## Поддержка

- 📖 [DNS_SETUP.md](DNS_SETUP.md) - Настройка DNS
- 📖 [MULTI_SITE_SETUP.md](MULTI_SITE_SETUP.md) - Работа с несколькими сайтами
- 📖 [DOMAIN_DEPLOYMENT_GUIDE.md](DOMAIN_DEPLOYMENT_GUIDE.md) - Полное руководство
- 📖 [QUICK_START.md](QUICK_START.md) - Быстрый старт

---

## Готово! 🎉

После выполнения всех шагов у вас будут работать:
- ✅ https://wbmoneyback.ru - существующий сайт
- ✅ https://maskbrowser.ru - новый основной сайт
- ✅ https://admin.maskbrowser.ru - админ панель

Все с SSL, автообновлением сертификатов и без конфликтов!
