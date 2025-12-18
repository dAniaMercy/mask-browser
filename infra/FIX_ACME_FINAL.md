# Финальное исправление ACME challenge

## Проблема

После замены `root` на `alias` nginx все еще возвращает 301 redirect.

## Возможные причины

1. Другие location блоки перехватывают запрос
2. Префикс `^~` не работает как ожидается
3. Нужно использовать более специфичный location блок

## Решение 1: Использовать точное совпадение

Попробуйте использовать точное совпадение вместо префикса:

```bash
# Для maskbrowser.ru
sudo nano /etc/nginx/sites-available/maskbrowser.ru.conf

# Заменить:
location ^~ /.well-known/acme-challenge/ {
    alias /var/www/certbot;
}

# На:
location = /.well-known/acme-challenge/ {
    alias /var/www/certbot;
    try_files $uri =404;
}

# Или использовать несколько вариантов:
location /.well-known/acme-challenge/ {
    alias /var/www/certbot;
    try_files $uri =404;
    access_log off;
}
```

## Решение 2: Проверить другие location блоки

Может быть другой location блок перехватывает запрос:

```bash
# Показать все location блоки
sudo grep -n "location" /etc/nginx/sites-available/maskbrowser.ru.conf
sudo grep -n "location" /etc/nginx/sites-available/admin.maskbrowser.ru.conf
```

Убедитесь, что нет других location блоков, которые могут перехватывать запрос до ACME challenge.

## Решение 3: Использовать полный путь

Попробуйте использовать полный путь в alias:

```nginx
location ^~ /.well-known/acme-challenge/ {
    alias /var/www/certbot/.well-known/acme-challenge/;
    try_files $uri =404;
}
```

Но это не рекомендуется, так как путь уже включает `.well-known/acme-challenge/`.

## Решение 4: Правильная конфигурация (рекомендуется)

Используйте эту конфигурацию:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name maskbrowser.ru www.maskbrowser.ru;

    # Let's Encrypt ACME challenge - ДОЛЖНО БЫТЬ ПЕРВЫМ
    location /.well-known/acme-challenge/ {
        alias /var/www/certbot;
        default_type text/plain;
        try_files $uri =404;
    }

    # Redirect all other traffic to HTTPS
    location / {
        return 301 https://maskbrowser.ru$request_uri;
    }
}
```

Ключевые моменты:
- Убрать `^~` (может вызывать проблемы)
- Добавить `default_type text/plain;`
- Добавить `try_files $uri =404;`
- Убедиться, что секция ПЕРВАЯ в блоке server

## Автоматическое исправление

```bash
cd /opt/mask-browser/infra
chmod +x scripts/fix-acme-final.sh
sudo bash scripts/fix-acme-final.sh
```

## Ручное исправление

```bash
# 1. Создать правильную секцию вручную
sudo nano /etc/nginx/sites-available/maskbrowser.ru.conf

# Найти секцию ACME challenge и заменить на:
location /.well-known/acme-challenge/ {
    alias /var/www/certbot;
    default_type text/plain;
    try_files $uri =404;
    access_log off;
}

# 2. Аналогично для admin.maskbrowser.ru
sudo nano /etc/nginx/sites-available/admin.maskbrowser.ru.conf

# 3. Проверить синтаксис
sudo nginx -t

# 4. Перезагрузить
sudo systemctl reload nginx

# 5. Проверить
echo "test" | sudo tee /var/www/certbot/test.txt
curl http://maskbrowser.ru/.well-known/acme-challenge/test.txt
sudo rm /var/www/certbot/test.txt
```

## Отладка

Если все еще не работает:

```bash
# 1. Проверить логи nginx
sudo tail -f /var/log/nginx/error.log

# 2. Проверить, что файл существует
ls -la /var/www/certbot/test.txt

# 3. Проверить права доступа
stat /var/www/certbot/test.txt

# 4. Проверить, может ли nginx прочитать файл
sudo -u www-data cat /var/www/certbot/test.txt

# 5. Проверить полную конфигурацию
sudo nginx -T | grep -A 10 "acme-challenge"
```

## Альтернатива: Использовать standalone режим

Если webroot не работает, можно использовать standalone режим (требует остановки nginx):

```bash
# Остановить nginx
sudo systemctl stop nginx

# Создать сертификат
sudo certbot certonly --standalone \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    -d maskbrowser.ru \
    -d www.maskbrowser.ru

# Запустить nginx
sudo systemctl start nginx
```

Но это не рекомендуется для production, так как требует остановки nginx.

