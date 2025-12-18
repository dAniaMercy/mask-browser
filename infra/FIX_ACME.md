# Исправление проблемы с ACME challenge

## Проблема

Certbot не может получить доступ к `.well-known/acme-challenge/`:
```
Invalid response from https://maskbrowser.ru/.well-known/acme-challenge/...: 404
```

Это означает, что nginx не настроен для обслуживания ACME challenge файлов.

## Быстрое исправление

```bash
cd /opt/mask-browser/infra
chmod +x scripts/fix-acme-challenge.sh
sudo bash scripts/fix-acme-challenge.sh
```

## Ручное исправление

### 1. Создать директорию для webroot

```bash
sudo mkdir -p /var/www/certbot
sudo chown -R www-data:www-data /var/www/certbot
sudo chmod -R 755 /var/www/certbot
```

### 2. Проверить конфигурацию nginx

Убедитесь, что в конфигах nginx есть секция для ACME challenge **в HTTP блоке (порт 80)**:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name maskbrowser.ru www.maskbrowser.ru;

    # Let's Encrypt ACME challenge
    location ^~ /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    # Redirect all other traffic to HTTPS
    location / {
        return 301 https://maskbrowser.ru$request_uri;
    }
}
```

### 3. Добавить секцию в конфиги (если отсутствует)

```bash
# Для maskbrowser.ru
sudo nano /etc/nginx/sites-available/maskbrowser.ru.conf

# Добавить в блок server для порта 80 (перед location /):
location ^~ /.well-known/acme-challenge/ {
    root /var/www/certbot;
}

# Для admin.maskbrowser.ru
sudo nano /etc/nginx/sites-available/admin.maskbrowser.ru.conf

# Добавить аналогичную секцию
```

### 4. Проверить и перезагрузить nginx

```bash
sudo nginx -t
sudo systemctl reload nginx
```

### 5. Проверить доступность

```bash
# Создать тестовый файл
echo "test" | sudo tee /var/www/certbot/test.txt

# Проверить доступность
curl http://maskbrowser.ru/.well-known/acme-challenge/test.txt
curl http://www.maskbrowser.ru/.well-known/acme-challenge/test.txt
curl http://admin.maskbrowser.ru/.well-known/acme-challenge/test.txt

# Должно вернуть "test"

# Удалить тестовый файл
sudo rm /var/www/certbot/test.txt
```

### 6. Создать сертификаты

После исправления конфигурации:

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

## Важно

1. **ACME challenge должен быть на порту 80 (HTTP)**, не на 443 (HTTPS)
2. **Секция должна быть ПЕРЕД redirect на HTTPS**, иначе она не будет работать
3. **Директория `/var/www/certbot` должна существовать** и быть доступна для nginx

## Проверка конфигурации

```bash
# Проверить, что секция есть в конфигах
grep -A 3 "acme-challenge" /etc/nginx/sites-available/maskbrowser.ru.conf
grep -A 3 "acme-challenge" /etc/nginx/sites-available/admin.maskbrowser.ru.conf

# Проверить синтаксис
sudo nginx -t

# Проверить логи
sudo tail -f /var/log/nginx/error.log
```
