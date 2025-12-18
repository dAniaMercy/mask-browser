# Исправление проблемы с путем ACME challenge

## Проблема

Nginx возвращает 301 redirect вместо содержимого файла при обращении к `.well-known/acme-challenge/`.

## Причина

Возможные причины:
1. Файл не найден (неправильный путь)
2. Nginx не может прочитать файл (права доступа)
3. Использование `root` вместо `alias` в некоторых случаях

## Быстрое исправление

```bash
cd /opt/mask-browser/infra
chmod +x scripts/fix-acme-path.sh
sudo bash scripts/fix-acme-path.sh
```

## Ручное исправление

### Вариант 1: Использовать alias вместо root

В некоторых случаях `alias` работает лучше, чем `root`:

```bash
# Для maskbrowser.ru
sudo nano /etc/nginx/sites-available/maskbrowser.ru.conf

# Заменить:
location ^~ /.well-known/acme-challenge/ {
    root /var/www/certbot;
}

# На:
location ^~ /.well-known/acme-challenge/ {
    alias /var/www/certbot;
}

# Для admin.maskbrowser.ru
sudo nano /etc/nginx/sites-available/admin.maskbrowser.ru.conf

# Аналогичная замена
```

### Вариант 2: Проверить и исправить права доступа

```bash
# Создать директорию
sudo mkdir -p /var/www/certbot

# Установить правильные права
sudo chown -R www-data:www-data /var/www/certbot
sudo chmod -R 755 /var/www/certbot

# Проверить
ls -la /var/www/certbot
```

### Вариант 3: Использовать полный путь в root

```nginx
location ^~ /.well-known/acme-challenge/ {
    root /var/www/certbot/.well-known/acme-challenge;
}
```

Но это не рекомендуется, лучше использовать `alias`.

## Проверка после исправления

```bash
# Создать тестовый файл
echo "test" | sudo tee /var/www/certbot/test.txt
sudo chown www-data:www-data /var/www/certbot/test.txt

# Проверить доступность
curl http://maskbrowser.ru/.well-known/acme-challenge/test.txt
curl http://admin.maskbrowser.ru/.well-known/acme-challenge/test.txt

# Должно вернуть "test", а не HTML с 301

# Удалить тестовый файл
sudo rm /var/www/certbot/test.txt
```

## Разница между root и alias

### root
```nginx
location ^~ /.well-known/acme-challenge/ {
    root /var/www/certbot;
}
# Запрос: http://domain/.well-known/acme-challenge/file.txt
# Ищет: /var/www/certbot/.well-known/acme-challenge/file.txt
```

### alias
```nginx
location ^~ /.well-known/acme-challenge/ {
    alias /var/www/certbot;
}
# Запрос: http://domain/.well-known/acme-challenge/file.txt
# Ищет: /var/www/certbot/file.txt
```

**Для ACME challenge лучше использовать `alias`**, так как путь уже включает `.well-known/acme-challenge/`.

## Полная правильная конфигурация

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name maskbrowser.ru www.maskbrowser.ru;

    # Let's Encrypt ACME challenge
    location ^~ /.well-known/acme-challenge/ {
        alias /var/www/certbot;
        try_files $uri =404;
    }

    # Redirect all other traffic to HTTPS
    location / {
        return 301 https://maskbrowser.ru$request_uri;
    }
}
```

Обратите внимание на:
- `alias` вместо `root`
- `try_files $uri =404;` для явной проверки существования файла

## После исправления

```bash
# Проверить синтаксис
sudo nginx -t

# Перезагрузить nginx
sudo systemctl reload nginx

# Создать сертификаты
sudo certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    -d maskbrowser.ru \
    -d www.maskbrowser.ru
```

