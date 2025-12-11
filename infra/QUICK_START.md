# Quick Start: Настройка доменов maskbrowser.ru

## TL;DR - Быстрая настройка

```bash
# 1. Настройте DNS у регистратора (Reg.ru, Nic.ru и т.д.)
# Добавьте A записи:
# @ → 109.172.101.73
# www → 109.172.101.73
# admin → 109.172.101.73

# 2. Подождите 10-15 минут, затем проверьте DNS
host maskbrowser.ru
host admin.maskbrowser.ru

# 3. На сервере запустите автоматическую настройку
ssh root@109.172.101.73
cd /opt/mask-browser/infra/scripts
chmod +x setup-domain.sh
sudo ./setup-domain.sh

# 4. Готово! Проверьте сайты
curl -I https://maskbrowser.ru
curl -I https://admin.maskbrowser.ru
```

## Что будет доступно

| URL | Сервис | Порт контейнера |
|-----|--------|-----------------|
| https://maskbrowser.ru | Client Web (React) | 5052 |
| https://admin.maskbrowser.ru | MaskAdmin (ASP.NET) | 5100 |

## Конфигурационные файлы

### Созданные файлы:

```
infra/
├── nginx/
│   ├── maskbrowser.ru.conf          # Nginx для основного сайта
│   └── admin.maskbrowser.ru.conf    # Nginx для админки
├── scripts/
│   └── setup-domain.sh              # Скрипт автоматической настройки
├── DNS_SETUP.md                     # Подробная инструкция по DNS
└── DOMAIN_DEPLOYMENT_GUIDE.md       # Полное руководство

MaskAdmin/
└── appsettings.Production.json      # Production конфигурация
```

## Файлы для коммита в Git

```bash
cd /opt/mask-browser

# Добавьте новые файлы
git add infra/nginx/maskbrowser.ru.conf
git add infra/nginx/admin.maskbrowser.ru.conf
git add infra/scripts/setup-domain.sh
git add infra/DNS_SETUP.md
git add infra/DOMAIN_DEPLOYMENT_GUIDE.md
git add infra/QUICK_START.md
git add MaskAdmin/appsettings.Production.json

# Закоммитьте
git commit -m "Add domain configuration for maskbrowser.ru"

# Запушьте
git push origin main
```

## Checklist

### Перед настройкой:
- [ ] Домен maskbrowser.ru куплен
- [ ] Есть доступ к панели управления DNS
- [ ] Есть SSH доступ к серверу 109.172.101.73
- [ ] Docker контейнеры запущены

### DNS настройка:
- [ ] A запись: @ → 109.172.101.73
- [ ] A запись: www → 109.172.101.73
- [ ] A запись: admin → 109.172.101.73
- [ ] DNS резолвятся (проверено через `host` команду)

### Сервер:
- [ ] Nginx установлен
- [ ] Certbot установлен (или будет установлен скриптом)
- [ ] Firewall настроен (порты 80, 443 открыты)
- [ ] Код обновлен из GitHub
- [ ] Docker контейнеры работают

### После настройки:
- [ ] SSL сертификаты получены
- [ ] https://maskbrowser.ru открывается
- [ ] https://admin.maskbrowser.ru открывается
- [ ] HTTP→HTTPS редирект работает
- [ ] www→non-www редирект работает
- [ ] Нет SSL ошибок в браузере
- [ ] Админка требует авторизацию
- [ ] Создан администратор

## Команды для проверки

```bash
# DNS
host maskbrowser.ru
host www.maskbrowser.ru
host admin.maskbrowser.ru

# SSL
openssl s_client -connect maskbrowser.ru:443 -servername maskbrowser.ru
openssl s_client -connect admin.maskbrowser.ru:443 -servername admin.maskbrowser.ru

# HTTP статусы
curl -I https://maskbrowser.ru
curl -I https://admin.maskbrowser.ru

# Docker
docker ps | grep maskbrowser

# Nginx
sudo nginx -t
sudo systemctl status nginx

# Логи
sudo tail -f /var/log/nginx/admin.maskbrowser.ru_access.log
docker-compose logs -f maskadmin
```

## Если что-то не работает

### DNS не резолвятся
- Подождите еще 30-60 минут
- Проверьте правильность записей у регистратора
- Используйте https://www.whatsmydns.net/ для проверки

### SSL не получен
- Убедитесь что DNS работают
- Проверьте логи: `sudo journalctl -u certbot`
- Попробуйте вручную: `sudo certbot certonly --webroot ...`

### 502 Bad Gateway
- Проверьте контейнеры: `docker ps`
- Перезапустите: `docker-compose restart maskadmin web`
- Проверьте порты: `netstat -tlnp | grep -E '5100|5052'`

### Админка не работает
- Проверьте логи контейнера: `docker logs maskbrowser-maskadmin`
- Проверьте БД подключение в `appsettings.Production.json`
- Создайте администратора: `./scripts/create-admin.sh`

## Поддержка

- 📖 Полная документация: [DOMAIN_DEPLOYMENT_GUIDE.md](DOMAIN_DEPLOYMENT_GUIDE.md)
- 🌐 Настройка DNS: [DNS_SETUP.md](DNS_SETUP.md)
- 🔐 Аутентификация: [../MaskAdmin/docs/AUTHENTICATION.md](../MaskAdmin/docs/AUTHENTICATION.md)
- 🚀 Деплой: [../MaskAdmin/DEPLOYMENT.md](../MaskAdmin/DEPLOYMENT.md)

## Контакты

Если возникли проблемы, проверьте:
1. Логи Nginx: `/var/log/nginx/*.log`
2. Логи Docker: `docker-compose logs`
3. Статус сервисов: `systemctl status nginx`
