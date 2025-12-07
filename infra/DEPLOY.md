# 🚀 Команды для развертывания оптимизированного решения

## ✅ Что было оптимизировано:

1. **Dockerfile.browser** - использует готовый Selenium образ
2. **start-websockify.sh** - упрощенный скрипт с проверками
3. **websockify.conf** - конфигурация supervisor для автоматического запуска
4. **React код** - исправлены ошибки #418 и #423

---

## 📋 Команды для применения изменений

### 1. Перейти в директорию проекта
```bash
cd /opt/mask-browser
```

### 2. Получить последние изменения из git
```bash
git pull origin main
# или
git pull origin master
```

### 3. Пересобрать образ браузера (ВАЖНО!)
```bash
cd infra
docker build -t maskbrowser/browser:latest -f Dockerfile.browser .
```

**Время сборки:** ~2-3 минуты (вместо 10-15 минут)

### 4. Пересобрать и перезапустить веб-контейнер
```bash
docker-compose up -d --build web
```

### 5. Остановить и удалить старые контейнеры профилей
```bash
# Остановить все контейнеры профилей
docker ps | grep maskbrowser-profile | awk '{print $1}' | xargs -r docker stop

# Удалить все контейнеры профилей
docker ps -a | grep maskbrowser-profile | awk '{print $1}' | xargs -r docker rm
```

### 6. Проверить, что образ собран правильно
```bash
docker images | grep maskbrowser/browser
```

Должен показать образ размером ~1.5GB

### 7. Проверить логи после создания нового профиля
```bash
# Найти контейнер профиля
docker ps | grep maskbrowser-profile

# Проверить логи (замените <ID> на ID контейнера)
docker logs maskbrowser-profile-<ID> --tail 100

# Проверить, что websockify запущен
docker exec maskbrowser-profile-<ID> ps aux | grep websockify

# Проверить, что порты слушаются
docker exec maskbrowser-profile-<ID> netstat -tlnp | grep -E ":(5900|6080)"
```

---

## 🔍 Проверка работоспособности

### Проверить, что все сервисы запущены:
```bash
# В контейнере профиля
docker exec maskbrowser-profile-<ID> supervisorctl status
```

Должно показать:
- `selenium-node` - running
- `vnc` - running  
- `websockify` - running

### Проверить доступность VNC через websockify:
```bash
# С хоста
curl -I http://109.172.101.73:<PORT>/vnc.html
```

Должен вернуть HTTP 200 или 302

---

## 🐛 Если что-то не работает

### Проблема: websockify не запускается
```bash
# Проверить логи supervisor
docker exec maskbrowser-profile-<ID> cat /var/log/websockify.err.log
docker exec maskbrowser-profile-<ID> cat /var/log/websockify.out.log

# Перезапустить websockify через supervisor
docker exec maskbrowser-profile-<ID> supervisorctl restart websockify
```

### Проблема: VNC не доступен
```bash
# Проверить, что VNC запущен
docker exec maskbrowser-profile-<ID> supervisorctl status vnc

# Перезапустить VNC
docker exec maskbrowser-profile-<ID> supervisorctl restart vnc
```

### Проблема: noVNC не найден
```bash
# Проверить наличие noVNC
docker exec maskbrowser-profile-<ID> ls -la /usr/share/novnc/

# Если нет, пересобрать образ
docker build -t maskbrowser/browser:latest -f Dockerfile.browser .
```

---

## 📊 Ожидаемые результаты

После применения изменений:

✅ **VNC работает** на порту 5900 (IPv4 и IPv6)  
✅ **websockify работает** на порту 6080  
✅ **noVNC доступен** по адресу `http://<IP>:<PORT>/vnc.html`  
✅ **React ошибки** #418 и #423 исправлены  
✅ **Быстрая сборка** образа (~2-3 минуты)  
✅ **Стабильная работа** всех сервисов  

---

## 🔄 Откат к старой версии (если нужно)

Если что-то пошло не так, можно вернуться к старой версии:

```bash
# Использовать старый Dockerfile (если сохранен)
docker build -t maskbrowser/browser:latest -f Dockerfile.browser.old .

# Или пересобрать с нуля
docker build -t maskbrowser/browser:latest -f Dockerfile.browser .
```

---

## 💡 Дополнительные оптимизации (опционально)

### 1. Использовать multi-stage build для уменьшения размера
```dockerfile
FROM selenium/standalone-chrome:latest AS base
# ... установка noVNC и websockify ...

FROM base AS final
# Копирование только необходимых файлов
```

### 2. Добавить health check
```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:6080/vnc.html || exit 1
```

### 3. Использовать .dockerignore
```
.git
*.md
node_modules
```

---

## ✅ Готово!

После выполнения всех команд система должна работать стабильно и быстро.

