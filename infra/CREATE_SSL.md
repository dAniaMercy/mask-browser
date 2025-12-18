# Создание SSL сертификатов

## Проблема

Сертификаты для `maskbrowser.ru` и `admin.maskbrowser.ru` отсутствуют. Nginx использует сертификат от `wbmoneyback.ru` для всех доменов.

## Быстрое создание сертификатов

```bash
cd /opt/mask-browser/infra
chmod +x scripts/create-ssl-certs.sh
sudo bash scripts/create-ssl-certs.sh
```

## Ручное создание

### 1. Создать сертификат для maskbrowser.ru

```bash
sudo certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    -d maskbrowser.ru \
    -d www.maskbrowser.ru
```

### 2. Создать сертификат для admin.maskbrowser.ru

```bash
sudo certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email admin@maskbrowser.ru \
    --agree-tos \
    --no-eff-email \
    -d admin.maskbrowser.ru
```

### 3. Проверить созданные сертификаты

```bash
sudo certbot certificates
```

### 4. Перезагрузить nginx

```bash
sudo systemctl reload nginx
```

### 5. Проверить SSL

```bash
# Проверить maskbrowser.ru
echo | openssl s_client -connect maskbrowser.ru:443 -servername maskbrowser.ru | grep "subject=CN"

# Проверить admin.maskbrowser.ru
echo | openssl s_client -connect admin.maskbrowser.ru:443 -servername admin.maskbrowser.ru | grep "subject=CN"
```

Должно показать:
- Для maskbrowser.ru: `subject=CN = maskbrowser.ru` или `subject=CN = www.maskbrowser.ru`
- Для admin.maskbrowser.ru: `subject=CN = admin.maskbrowser.ru`

## Требования перед созданием

1. **DNS записи должны быть настроены:**
   ```bash
   host maskbrowser.ru
   host www.maskbrowser.ru
   host admin.maskbrowser.ru
   # Все должны указывать на 109.172.101.73
   ```

2. **Порты 80 и 443 должны быть открыты:**
   ```bash
   sudo ufw status | grep -E '80|443'
   ```

3. **Nginx должен быть настроен для ACME challenge:**
   ```nginx
   location ^~ /.well-known/acme-challenge/ {
       root /var/www/certbot;
   }
   ```

4. **Директория для webroot должна существовать:**
   ```bash
   sudo mkdir -p /var/www/certbot
   ```

## Проверка после создания

```bash
# Проверить все сертификаты
sudo certbot certificates

# Проверить SSL для каждого домена
echo | openssl s_client -connect maskbrowser.ru:443 -servername maskbrowser.ru | grep -E "subject=CN|Verify return code"
echo | openssl s_client -connect admin.maskbrowser.ru:443 -servername admin.maskbrowser.ru | grep -E "subject=CN|Verify return code"

# Проверить через curl (без проверки сертификата)
curl -k -I https://maskbrowser.ru
curl -k -I https://admin.maskbrowser.ru
```

## Автоматическое обновление

```bash
# Настроить автообновление
sudo bash -c 'cat > /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh <<EOF
#!/bin/bash
systemctl reload nginx
EOF'

sudo chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh

# Тест автообновления
sudo certbot renew --dry-run
```

## Troubleshooting

### Ошибка: "Failed to connect"

1. Проверьте DNS:
   ```bash
   host maskbrowser.ru
   ```

2. Проверьте, что nginx работает:
   ```bash
   sudo systemctl status nginx
   ```

3. Проверьте, что порт 80 открыт:
   ```bash
   sudo netstat -tulpn | grep :80
   ```

### Ошибка: "Connection refused"

1. Проверьте firewall:
   ```bash
   sudo ufw allow 80/tcp
   sudo ufw allow 443/tcp
   ```

2. Проверьте, что nginx слушает порт 80:
   ```bash
   sudo nginx -t
   sudo systemctl reload nginx
   ```

### Сертификат создан, но не используется

1. Проверьте конфигурацию nginx:
   ```bash
   grep ssl_certificate /etc/nginx/sites-available/maskbrowser.ru.conf
   ```

2. Убедитесь, что пути правильные:
   ```bash
   ls -la /etc/letsencrypt/live/maskbrowser.ru/
   ```

3. Перезагрузите nginx:
   ```bash
   sudo systemctl reload nginx
   ```
