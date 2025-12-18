# Отладка проблемы с ACME challenge

## Запуск отладки

```bash
cd /opt/mask-browser/infra
chmod +x scripts/debug-acme.sh
sudo bash scripts/debug-acme.sh
```

## Ручная проверка

### 1. Проверить реальную конфигурацию

```bash
# Показать HTTP блок полностью
sudo cat /etc/nginx/sites-available/maskbrowser.ru.conf | grep -A 30 "listen 80"

# Показать секцию ACME challenge
sudo grep -A 5 "acme-challenge" /etc/nginx/sites-available/maskbrowser.ru.conf
```

### 2. Проверить все location блоки

```bash
# Показать все location блоки с номерами строк
sudo grep -n "location" /etc/nginx/sites-available/maskbrowser.ru.conf
```

### 3. Проверить файл и права

```bash
# Проверить файл
ls -la /var/www/certbot/test.txt

# Проверить, может ли www-data прочитать
sudo -u www-data cat /var/www/certbot/test.txt
```

### 4. Тест с подробным выводом

```bash
# Тест с verbose
curl -v http://maskbrowser.ru/.well-known/acme-challenge/test.txt

# Посмотреть заголовки
curl -I http://maskbrowser.ru/.well-known/acme-challenge/test.txt
```

### 5. Проверить логи nginx

```bash
# Посмотреть ошибки
sudo tail -f /var/log/nginx/error.log

# В другом терминале выполнить запрос
curl http://maskbrowser.ru/.well-known/acme-challenge/test.txt
```

## Возможные решения

### Решение 1: Использовать root вместо alias

Если alias не работает, попробуйте root с правильным путем:

```nginx
location /.well-known/acme-challenge/ {
    root /var/www;
    try_files $uri =404;
}
```

В этом случае файл должен быть в `/var/www/.well-known/acme-challenge/`, а не в `/var/www/certbot/`.

### Решение 2: Использовать точное совпадение

```nginx
location = /.well-known/acme-challenge/ {
    return 404;
}

location = /.well-known/acme-challenge/test.txt {
    alias /var/www/certbot/test.txt;
}
```

Но это не подходит для динамических файлов certbot.

### Решение 3: Проверить, нет ли других location блоков

Может быть другой location блок перехватывает запрос. Проверьте:

```bash
# Показать все location блоки
sudo grep -n "location" /etc/nginx/sites-available/maskbrowser.ru.conf
```

Убедитесь, что нет location блоков типа:
- `location /` перед ACME challenge
- `location ~` который может перехватывать
- `location ^~ /` который может перехватывать

### Решение 4: Использовать другой путь

Попробуйте использовать другой путь для webroot:

```bash
# Создать директорию в другом месте
sudo mkdir -p /var/www/html/.well-known/acme-challenge
sudo chown -R www-data:www-data /var/www/html/.well-known

# В конфиге использовать:
location /.well-known/acme-challenge/ {
    root /var/www/html;
    try_files $uri =404;
}
```

### Решение 5: Использовать standalone режим certbot

Если ничего не помогает, используйте standalone режим (требует остановки nginx):

```bash
# Остановить nginx
sudo systemctl stop nginx

# Создать сертификаты
sudo certbot certonly --standalone \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    -d maskbrowser.ru \
    -d www.maskbrowser.ru

sudo certbot certonly --standalone \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    -d admin.maskbrowser.ru

# Запустить nginx
sudo systemctl start nginx
```

## Проверка после исправления

```bash
# Создать тестовый файл
echo "test" | sudo tee /var/www/certbot/test.txt
sudo chown www-data:www-data /var/www/certbot/test.txt

# Проверить
curl http://maskbrowser.ru/.well-known/acme-challenge/test.txt

# Должно вернуть "test", а не HTML с 301
```

