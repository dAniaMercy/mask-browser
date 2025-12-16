# Исправление проблемы с маршрутизацией nginx

## Проблема
При переходе на `admin.maskbrowser.ru` или `maskbrowser.ru` происходит редирект на `wbmoneyback.ru`.

## Причина
Когда несколько `server` блоков слушают один порт (443) без `default_server`, nginx использует первый блок, который соответствует запросу. Если ни один не соответствует точно по `server_name`, используется первый блок по алфавитному порядку файлов.

## Решение

### Вариант 1: Удалить default_server из wbmoneyback.ru (если есть)

```bash
# 1. Проверить, есть ли default_server в wbmoneyback.ru
grep "default_server" /etc/nginx/sites-available/wbmoneyback.ru

# 2. Если есть, удалить его
sudo sed -i 's/ listen 443 ssl default_server;/ listen 443 ssl;/g' /etc/nginx/sites-available/wbmoneyback.ru
sudo sed -i 's/ listen \[::\]:443 ssl default_server;/ listen [::]:443 ssl;/g' /etc/nginx/sites-available/wbmoneyback.ru
sudo sed -i 's/ listen 443 ssl http2 default_server;/ listen 443 ssl http2;/g' /etc/nginx/sites-available/wbmoneyback.ru
sudo sed -i 's/ listen \[::\]:443 ssl http2 default_server;/ listen [::]:443 ssl http2;/g' /etc/nginx/sites-available/wbmoneyback.ru

# 3. Проверить синтаксис
sudo nginx -t

# 4. Перезагрузить nginx
sudo systemctl reload nginx
```

### Вариант 2: Использовать префиксы для правильного порядка загрузки

```bash
# 1. Переименовать конфиги с префиксами
cd /etc/nginx/sites-enabled
sudo mv admin.maskbrowser.ru.conf 01-admin.maskbrowser.ru.conf
sudo mv maskbrowser.ru.conf 00-maskbrowser.ru.conf
sudo mv wbmoneyback.ru 99-wbmoneyback.ru

# 2. Проверить синтаксис
sudo nginx -t

# 3. Перезагрузить nginx
sudo systemctl reload nginx
```

### Вариант 3: Добавить default_server к правильному конфигу (не рекомендуется)

Если вы хотите, чтобы `maskbrowser.ru` был default, добавьте `default_server` к его конфигу:

```bash
# Отредактировать /etc/nginx/sites-available/maskbrowser.ru.conf
sudo nano /etc/nginx/sites-available/maskbrowser.ru.conf

# Изменить строку:
# listen 443 ssl http2;
# на:
# listen 443 ssl http2 default_server;

# Проверить и перезагрузить
sudo nginx -t
sudo systemctl reload nginx
```

## Диагностика

### Проверить текущую конфигурацию

```bash
# 1. Посмотреть все конфиги
ls -la /etc/nginx/sites-enabled/

# 2. Проверить default_server
grep -r "default_server" /etc/nginx/sites-enabled/

# 3. Посмотреть полную конфигурацию nginx
sudo nginx -T | grep -A 10 "server_name maskbrowser.ru"
sudo nginx -T | grep -A 10 "server_name admin.maskbrowser.ru"
sudo nginx -T | grep -A 10 "server_name wbmoneyback.ru"

# 4. Проверить логи
sudo tail -f /var/log/nginx/error.log
```

### Проверить работу сайтов

```bash
# Проверить заголовки ответов
curl -I https://maskbrowser.ru
curl -I https://admin.maskbrowser.ru
curl -I https://wbmoneyback.ru

# Проверить с указанием Host заголовка
curl -I -H "Host: maskbrowser.ru" https://109.172.101.73
curl -I -H "Host: admin.maskbrowser.ru" https://109.172.101.73
```

## Автоматическое исправление

Используйте скрипт для автоматической диагностики и исправления:

```bash
# Загрузить скрипт на сервер
# Затем выполнить:
chmod +x fix-nginx-routing.sh
sudo ./fix-nginx-routing.sh
```

## Проверка после исправления

После исправления проверьте:

1. **Работают ли сайты:**
   ```bash
   curl -I https://maskbrowser.ru
   curl -I https://admin.maskbrowser.ru
   ```

2. **Нет ли редиректов:**
   - Откройте браузер и перейдите на `https://maskbrowser.ru`
   - Должен открыться сайт MaskBrowser, а не wbmoneyback.ru

3. **Проверьте логи:**
   ```bash
   sudo tail -f /var/log/nginx/maskbrowser.ru_access.log
   sudo tail -f /var/log/nginx/admin.maskbrowser.ru_access.log
   ```

## Важные замечания

1. **SNI (Server Name Indication):** Nginx использует SNI для определения, какой `server` блок использовать. Убедитесь, что SSL сертификаты правильно настроены для каждого домена.

2. **Порядок загрузки:** Nginx загружает конфиги в алфавитном порядке. Используйте префиксы (00-, 01-, 99-) для контроля порядка.

3. **default_server:** Используйте `default_server` только для catch-all блока, который должен обрабатывать запросы, не соответствующие ни одному `server_name`.

4. **Проверка SSL:** Убедитесь, что SSL сертификаты существуют и действительны:
   ```bash
   sudo certbot certificates
   ```
