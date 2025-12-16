# Исправление проблем с SSL сертификатами

## Проблема

Ошибка при проверке SSL:
```
curl: (60) SSL: no alternative certificate subject name matches target host name
```

Это означает, что:
- SSL сертификаты отсутствуют
- Сертификаты не соответствуют доменам
- Сертификаты истекли или недействительны

## Быстрое исправление

```bash
cd /opt/mask-browser/infra
chmod +x scripts/fix-ssl.sh
sudo bash scripts/fix-ssl.sh
```

## Ручное исправление

### 1. Проверить наличие сертификатов

```bash
# Проверить все сертификаты
sudo certbot certificates

# Проверить конкретный сертификат
ls -la /etc/letsencrypt/live/maskbrowser.ru/
ls -la /etc/letsencrypt/live/admin.maskbrowser.ru/
```

### 2. Создать/обновить сертификаты

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

### 3. Проверить конфигурацию nginx

```bash
# Проверить синтаксис
sudo nginx -t

# Перезагрузить nginx
sudo systemctl reload nginx
```

### 4. Проверить SSL

```bash
# Проверить через openssl
echo | openssl s_client -connect maskbrowser.ru:443 -servername maskbrowser.ru | grep "Verify return code"

# Проверить через curl (без проверки сертификата)
curl -k -I https://maskbrowser.ru
curl -k -I https://admin.maskbrowser.ru
```

## Проверка DNS

Убедитесь, что DNS записи настроены правильно:

```bash
# Проверить DNS
host maskbrowser.ru
host admin.maskbrowser.ru

# Должно быть: 109.172.101.73
```

## Автоматическое обновление сертификатов

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

### Сертификаты не создаются

1. **Проверьте DNS:**
   ```bash
   host maskbrowser.ru
   # Должно быть: 109.172.101.73
   ```

2. **Проверьте порты:**
   ```bash
   sudo ufw status | grep -E '80|443'
   # Порты 80 и 443 должны быть открыты
   ```

3. **Проверьте nginx:**
   ```bash
   sudo nginx -t
   sudo systemctl status nginx
   ```

### Сертификаты созданы, но не работают

1. **Проверьте пути в конфигах nginx:**
   ```bash
   grep ssl_certificate /etc/nginx/sites-available/maskbrowser.ru.conf
   grep ssl_certificate /etc/nginx/sites-available/admin.maskbrowser.ru.conf
   ```

2. **Проверьте права доступа:**
   ```bash
   ls -la /etc/letsencrypt/live/
   # Должны быть символические ссылки на актуальные сертификаты
   ```

3. **Перезагрузите nginx:**
   ```bash
   sudo systemctl reload nginx
   ```

### Проверка без проверки сертификата (для тестирования)

```bash
# Использовать -k флаг для curl
curl -k -I https://maskbrowser.ru
curl -k -I https://admin.maskbrowser.ru

# Или использовать --insecure
curl --insecure -I https://maskbrowser.ru
```

**Внимание:** Флаг `-k` отключает проверку SSL сертификата. Используйте только для диагностики!
