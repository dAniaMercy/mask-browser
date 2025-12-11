# Настройка нескольких сайтов на одном сервере

## Проблема

На сервере уже есть другой сайт с доменом, и нужно добавить maskbrowser.ru без конфликтов.

## Решение

Nginx может обслуживать множество доменов на одном сервере через **server_name** директиву.

---

## Шаг 1: Проверьте существующие конфиги

```bash
# Проверить все активные конфиги
ls -la /etc/nginx/sites-enabled/

# Проверить какие домены уже настроены
grep -r "server_name" /etc/nginx/sites-enabled/

# Проверить default_server директивы
grep -r "default_server" /etc/nginx/sites-enabled/
```

### Что искать:

1. **default_server** - этот сайт будет отвечать на все неизвестные домены
2. **server_name** - какие домены обслуживаются
3. **listen 80** и **listen 443** - какие порты используются

---

## Шаг 2: Убедитесь, что нет конфликтов

### Проблема: Один из сайтов помечен как default_server

Nginx позволяет только **один** default_server на каждом порту.

**Проверьте:**
```bash
grep -n "default_server" /etc/nginx/sites-enabled/*
```

**Если найдено:**
```
/etc/nginx/sites-enabled/other-site.conf:3:    listen 80 default_server;
/etc/nginx/sites-enabled/other-site.conf:4:    listen [::]:80 default_server;
```

**Решение:**

Вариант 1: Оставьте `default_server` у существующего сайта
- MaskBrowser будет доступен только по своему домену
- Запросы к неизвестным доменам пойдут на существующий сайт

Вариант 2: Удалите `default_server` у существующего сайта
- Создайте отдельный `default` конфиг, который возвращает 444 (закрывает соединение)
- Оба сайта будут доступны только по своим доменам

---

## Шаг 3: Структура конфигов

Рекомендуемая структура:

```
/etc/nginx/sites-available/
├── default                       # Обработчик неизвестных доменов (опционально)
├── other-site.com.conf          # Ваш существующий сайт
├── maskbrowser.ru.conf          # Основной сайт MaskBrowser
└── admin.maskbrowser.ru.conf    # Админка MaskBrowser

/etc/nginx/sites-enabled/
├── default -> ../sites-available/default
├── other-site.com.conf -> ../sites-available/other-site.com.conf
├── maskbrowser.ru.conf -> ../sites-available/maskbrowser.ru.conf
└── admin.maskbrowser.ru.conf -> ../sites-available/admin.maskbrowser.ru.conf
```

---

## Шаг 4: Создайте default обработчик (рекомендуется)

Этот конфиг будет отклонять запросы к неизвестным доменам:

```nginx
# /etc/nginx/sites-available/default

# HTTP - возвращаем 444 для неизвестных доменов
server {
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name _;
    return 444;
}

# HTTPS - возвращаем 444 для неизвестных доменов
server {
    listen 443 ssl default_server;
    listen [::]:443 ssl default_server;
    server_name _;

    # Используйте любой валидный сертификат (от существующего сайта)
    ssl_certificate /etc/letsencrypt/live/your-existing-domain/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/your-existing-domain/privkey.pem;

    return 444;
}
```

**Установка:**
```bash
sudo nano /etc/nginx/sites-available/default
# Вставьте конфиг выше

sudo ln -sf /etc/nginx/sites-available/default /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Шаг 5: Убедитесь что server_name уникальны

Каждый сайт должен иметь уникальный `server_name`:

### Существующий сайт (example.com):
```nginx
server {
    listen 443 ssl http2;
    server_name example.com www.example.com;
    # ...
}
```

### MaskBrowser (maskbrowser.ru):
```nginx
server {
    listen 443 ssl http2;
    server_name maskbrowser.ru www.maskbrowser.ru;
    # ...
}
```

### Admin MaskBrowser (admin.maskbrowser.ru):
```nginx
server {
    listen 443 ssl http2;
    server_name admin.maskbrowser.ru;
    # ...
}
```

**Важно:** Nginx маршрутизирует по `server_name`, поэтому домены не должны пересекаться.

---

## Шаг 6: SSL сертификаты для нескольких доменов

Каждый домен получает свой сертификат:

```bash
# Существующий домен (если еще нет)
sudo certbot certonly --webroot \
    -w /var/www/certbot \
    -d example.com \
    -d www.example.com

# MaskBrowser
sudo certbot certonly --webroot \
    -w /var/www/certbot \
    -d maskbrowser.ru \
    -d www.maskbrowser.ru

# Admin MaskBrowser
sudo certbot certonly --webroot \
    -w /var/www/certbot \
    -d admin.maskbrowser.ru
```

Certbot автоматически обновит **все** сертификаты при запуске `certbot renew`.

---

## Шаг 7: Проверка конфигурации

```bash
# Тест синтаксиса Nginx
sudo nginx -t

# Если ошибки:
# 1. Проверьте что нет дублирующихся default_server
# 2. Проверьте что server_name уникальны
# 3. Проверьте что SSL сертификаты существуют

# Перезагрузка
sudo systemctl reload nginx
```

---

## Шаг 8: Проверка доступности

```bash
# Проверьте каждый домен
curl -I https://example.com
curl -I https://maskbrowser.ru
curl -I https://admin.maskbrowser.ru

# Проверьте неизвестный домен (должен вернуть 444 или ошибку)
curl -I http://109.172.101.73
```

---

## Пример: Два сайта на одном сервере

### Сайт 1: example.com (уже существует)

```nginx
# /etc/nginx/sites-available/example.com.conf

server {
    listen 80;
    server_name example.com www.example.com;
    return 301 https://example.com$request_uri;
}

server {
    listen 443 ssl http2;
    server_name example.com www.example.com;

    ssl_certificate /etc/letsencrypt/live/example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/example.com/privkey.pem;

    root /var/www/example.com;
    index index.html;
}
```

### Сайт 2: maskbrowser.ru (новый)

```nginx
# /etc/nginx/sites-available/maskbrowser.ru.conf

server {
    listen 80;
    server_name maskbrowser.ru www.maskbrowser.ru;
    return 301 https://maskbrowser.ru$request_uri;
}

server {
    listen 443 ssl http2;
    server_name maskbrowser.ru www.maskbrowser.ru;

    ssl_certificate /etc/letsencrypt/live/maskbrowser.ru/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/maskbrowser.ru/privkey.pem;

    location / {
        proxy_pass http://localhost:5052;
        # proxy settings...
    }
}
```

Оба сайта работают параллельно на портах 80/443!

---

## Troubleshooting

### Ошибка: "conflicting server name"

```
nginx: [emerg] conflicting server name "example.com" on 0.0.0.0:443
```

**Причина:** Два конфига пытаются обслуживать один домен.

**Решение:**
```bash
# Найдите дубликаты
grep -r "server_name example.com" /etc/nginx/sites-enabled/

# Удалите или отключите лишний конфиг
sudo rm /etc/nginx/sites-enabled/duplicate-config.conf
```

### Ошибка: "duplicate default_server"

```
nginx: [emerg] a duplicate default server for 0.0.0.0:80
```

**Причина:** Несколько конфигов имеют `default_server`.

**Решение:**
```bash
# Найдите все default_server
grep -r "default_server" /etc/nginx/sites-enabled/

# Оставьте только один (в default или основном сайте)
# Удалите default_server из остальных конфигов
```

### Сайт открывается, но показывает не тот контент

**Причина:** Неправильный `server_name` или нет SSL для домена.

**Решение:**
```bash
# Проверьте какой конфиг обрабатывает запрос
curl -I https://maskbrowser.ru

# Проверьте server_name в конфиге
grep "server_name" /etc/nginx/sites-enabled/maskbrowser.ru.conf
```

---

## Рекомендуемая последовательность

1. **Сохраните существующий сайт:** Не трогайте его конфиг
2. **Добавьте MaskBrowser конфиги:** В отдельные файлы
3. **Получите SSL для MaskBrowser:** Отдельные сертификаты
4. **Протестируйте:** `nginx -t`
5. **Перезагрузите:** `systemctl reload nginx`
6. **Проверьте оба сайта:** Должны работать параллельно

---

## Команды для диагностики

```bash
# Список всех server блоков
sudo nginx -T | grep "server_name" -A 5

# Список всех прослушиваемых портов
sudo nginx -T | grep "listen"

# Какие процессы используют порт 80/443
sudo netstat -tlnp | grep -E ':80|:443'

# Проверка какой конфиг обслуживает домен
curl -I -H "Host: maskbrowser.ru" http://localhost
```

---

## Итого

✅ Nginx может обслуживать **неограниченное количество** доменов на одном IP
✅ Маршрутизация происходит по `server_name`
✅ Каждый домен может иметь свой SSL сертификат
✅ Все сайты работают параллельно без конфликтов

**Главное правило:** Уникальные `server_name` для каждого сайта!
