# Проверка конфигурации nginx

## Запуск диагностики

```bash
cd /opt/mask-browser/infra
chmod +x scripts/check-nginx-config.sh
sudo bash scripts/check-nginx-config.sh
```

## Ручная проверка

### 1. Проверить конфигурацию maskbrowser.ru

```bash
# Показать HTTP блок (порт 80)
sudo cat /etc/nginx/sites-available/maskbrowser.ru.conf | grep -A 20 "listen 80"

# Проверить наличие ACME challenge
sudo grep -A 3 "acme-challenge" /etc/nginx/sites-available/maskbrowser.ru.conf

# Проверить порядок location блоков
sudo grep -n "location" /etc/nginx/sites-available/maskbrowser.ru.conf
```

### 2. Проверить конфигурацию admin.maskbrowser.ru

```bash
# Показать HTTP блок (порт 80)
sudo cat /etc/nginx/sites-available/admin.maskbrowser.ru.conf | grep -A 20 "listen 80"

# Проверить наличие ACME challenge
sudo grep -A 3 "acme-challenge" /etc/nginx/sites-available/admin.maskbrowser.ru.conf
```

### 3. Проверить порядок location блоков

**Важно:** Секция ACME challenge должна быть **ПЕРЕД** `location /`, иначе она не будет работать!

Правильный порядок:
```nginx
server {
    listen 80;
    server_name maskbrowser.ru;

    # ACME challenge ПЕРЕД location /
    location ^~ /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    # Redirect ПОСЛЕ ACME challenge
    location / {
        return 301 https://maskbrowser.ru$request_uri;
    }
}
```

Неправильный порядок (не будет работать):
```nginx
server {
    listen 80;
    server_name maskbrowser.ru;

    # Redirect ПЕРЕД ACME challenge - НЕПРАВИЛЬНО!
    location / {
        return 301 https://maskbrowser.ru$request_uri;
    }

    # ACME challenge ПОСЛЕ location / - не будет работать
    location ^~ /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }
}
```

### 4. Проверить директорию webroot

```bash
# Проверить существование
ls -la /var/www/certbot

# Проверить права
stat /var/www/certbot

# Должно быть: владелец www-data:www-data или root:root
```

### 5. Тест доступности

```bash
# Создать тестовый файл
echo "test" | sudo tee /var/www/certbot/test.txt

# Проверить доступность через HTTP (не HTTPS!)
curl http://maskbrowser.ru/.well-known/acme-challenge/test.txt
curl http://www.maskbrowser.ru/.well-known/acme-challenge/test.txt
curl http://admin.maskbrowser.ru/.well-known/acme-challenge/test.txt

# Должно вернуть "test"

# Удалить тестовый файл
sudo rm /var/www/certbot/test.txt
```

## Типичные проблемы

### Проблема 1: ACME challenge после location /

**Симптом:** HTTP 404 при обращении к ACME challenge

**Решение:** Переместить секцию ACME challenge ПЕРЕД location /

### Проблема 2: Директория не существует

**Симптом:** HTTP 404 или 403

**Решение:**
```bash
sudo mkdir -p /var/www/certbot
sudo chown -R www-data:www-data /var/www/certbot
sudo chmod -R 755 /var/www/certbot
```

### Проблема 3: Неправильный путь root

**Симптом:** HTTP 404

**Решение:** Убедитесь, что в конфиге указан правильный путь:
```nginx
location ^~ /.well-known/acme-challenge/ {
    root /var/www/certbot;  # Правильно
    # НЕ root /var/www/certbot/; (слеш в конце)
}
```

### Проблема 4: Конфликт с default конфигом

**Симптом:** Запросы идут не туда

**Решение:** Проверить, нет ли default_server в других конфигах:
```bash
grep -r "default_server" /etc/nginx/sites-enabled/
```

## Исправление конфигов

Если конфиги неправильные, можно скопировать правильные из репозитория:

```bash
# Создать резервную копию
sudo cp /etc/nginx/sites-available/maskbrowser.ru.conf /etc/nginx/sites-available/maskbrowser.ru.conf.backup

# Скопировать из репозитория (если есть)
sudo cp /opt/mask-browser/infra/nginx/maskbrowser.ru.conf /etc/nginx/sites-available/maskbrowser.ru.conf

# Проверить и перезагрузить
sudo nginx -t
sudo systemctl reload nginx
```
