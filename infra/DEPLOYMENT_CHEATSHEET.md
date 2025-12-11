# MaskBrowser Deployment - Шпаргалка

## ⚡ Быстрый старт (TL;DR)

```bash
# 1. Настройте DNS у регистратора (10 минут для распространения)
# @ → 109.172.101.73
# www → 109.172.101.73
# admin → 109.172.101.73

# 2. На сервере
ssh root@109.172.101.73
cd /opt/mask-browser
git pull origin main

# 3. Запустите скрипт
cd infra/scripts
chmod +x setup-domain-safe.sh
sudo ./setup-domain-safe.sh

# 4. Создайте админа
cd ../../MaskAdmin/scripts
./create-admin.sh "Admin123!"

# 5. Готово! Откройте:
# https://maskbrowser.ru
# https://admin.maskbrowser.ru
```

---

## 📋 DNS Records

| Тип | Имя | Значение | Где |
|-----|-----|----------|-----|
| A | @ | 109.172.101.73 | Регистратор домена |
| A | www | 109.172.101.73 | Регистратор домена |
| A | admin | 109.172.101.73 | Регистратор домена |

**Проверка:**
```bash
host maskbrowser.ru  # → 109.172.101.73
```

---

## 🚀 Команды на сервере

### Обновление кода
```bash
cd /opt/mask-browser
git pull origin main
```

### Проверка существующих сайтов
```bash
cd /opt/mask-browser/infra/scripts
./check-existing-sites.sh
```

### Автоматическая настройка
```bash
cd /opt/mask-browser/infra/scripts
sudo ./setup-domain-safe.sh
```

### Создание администратора
```bash
cd /opt/mask-browser/MaskAdmin/scripts
./create-admin.sh "YourPassword123!"
```

### Перезапуск контейнеров
```bash
cd /opt/mask-browser/infra
docker-compose restart maskadmin web
```

---

## 🔍 Проверка

### Доступность сайтов
```bash
curl -I https://wbmoneyback.ru        # Старый сайт
curl -I https://maskbrowser.ru         # Новый сайт
curl -I https://admin.maskbrowser.ru   # Админка
```

### SSL сертификаты
```bash
sudo certbot certificates
```

### Логи
```bash
# Nginx
sudo tail -f /var/log/nginx/admin.maskbrowser.ru_access.log

# Docker
docker-compose logs -f maskadmin
```

### Статус
```bash
# Контейнеры
docker ps | grep maskbrowser

# Nginx
sudo systemctl status nginx
```

---

## 🔧 Troubleshooting

### DNS не работает
```bash
# Проверка
host maskbrowser.ru

# Если не резолвится - подождите 30-60 минут
```

### Nginx ошибка
```bash
# Проверка конфига
sudo nginx -t

# Перезагрузка
sudo systemctl reload nginx
```

### SSL ошибка
```bash
# Пересоздать сертификат
sudo certbot certonly --force-renewal -d maskbrowser.ru -d www.maskbrowser.ru
```

### 502 Bad Gateway
```bash
# Проверить контейнер
docker ps | grep maskadmin
docker-compose restart maskadmin
docker-compose logs maskadmin
```

### Админка: "Invalid username or password"
```bash
# Сбросить пароль
cd /opt/mask-browser/MaskAdmin/scripts
./reset-password.sh "Admin123!"
```

---

## 📁 Файлы

### Конфиги Nginx
- `/etc/nginx/sites-available/maskbrowser.ru.conf`
- `/etc/nginx/sites-available/admin.maskbrowser.ru.conf`

### Production конфиг
- `/opt/mask-browser/MaskAdmin/appsettings.Production.json`

### Логи
- `/var/log/nginx/maskbrowser.ru_access.log`
- `/var/log/nginx/admin.maskbrowser.ru_access.log`
- `/opt/mask-browser/MaskAdmin/logs/`

### SSL сертификаты
- `/etc/letsencrypt/live/maskbrowser.ru/`
- `/etc/letsencrypt/live/admin.maskbrowser.ru/`

---

## 🌐 URLs после развертывания

| URL | Назначение | Порт |
|-----|------------|------|
| https://maskbrowser.ru | Client Web | 5052 |
| https://admin.maskbrowser.ru | Admin Panel | 5100 |
| https://wbmoneyback.ru | Existing Site | - |

---

## 🔐 Учетные данные

### Админка
- URL: https://admin.maskbrowser.ru/Auth/Login
- Username: `admin`
- Password: `Admin123!` (или ваш)

### PostgreSQL
- Host: `maskbrowser-postgres` (внутри Docker)
- Database: `maskbrowser`
- Username: `maskuser`
- Password: `maskpass123`

---

## 📞 Webhook URLs

После развертывания укажите в платежных системах:

**CryptoBot:**
```
https://admin.maskbrowser.ru/api/webhook/cryptobot
```

**Bybit:**
```
https://admin.maskbrowser.ru/api/webhook/bybit
```

**Обновите секреты** в:
```bash
/opt/mask-browser/MaskAdmin/appsettings.Production.json
```

---

## ✅ Checklist

- [ ] DNS настроены
- [ ] DNS резолвятся (проверено)
- [ ] Код обновлен (`git pull`)
- [ ] Скрипт запущен
- [ ] SSL получен
- [ ] https://maskbrowser.ru работает
- [ ] https://admin.maskbrowser.ru работает
- [ ] https://wbmoneyback.ru продолжает работать
- [ ] Администратор создан
- [ ] Вход в админку работает
- [ ] Webhooks настроены

---

## 📚 Документация

- [FINAL_DEPLOYMENT_STEPS.md](FINAL_DEPLOYMENT_STEPS.md) - Пошаговое руководство
- [DNS_SETUP.md](DNS_SETUP.md) - Настройка DNS
- [MULTI_SITE_SETUP.md](MULTI_SITE_SETUP.md) - Несколько сайтов
- [QUICK_START.md](QUICK_START.md) - Быстрый старт

---

## 🆘 Помощь

Если что-то не работает:

1. Проверьте логи Nginx и Docker
2. Убедитесь что DNS распространились
3. Проверьте статус контейнеров
4. См. раздел Troubleshooting выше
