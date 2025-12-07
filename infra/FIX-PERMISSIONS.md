# 🔧 Исправление ошибки EACCES для websockify

## ❌ Проблема:

```
INFO spawnerr: unknown error making dispatchers for 'websockify': EACCES
websockify                       FATAL     unknown error making dispatchers for 'websockify': EACCES
```

Это ошибка прав доступа - supervisor не может запустить websockify от имени пользователя `seluser`.

---

## ✅ Решение:

### 1. Пересобрать образ БЕЗ кэша:

```bash
cd /opt/mask-browser/infra
docker build --no-cache -t maskbrowser/browser:latest -f Dockerfile.browser .
```

### 2. Остановить и удалить старые контейнеры:

```bash
docker ps | grep maskbrowser-profile | awk '{print $1}' | xargs -r docker stop
docker ps -a | grep maskbrowser-profile | awk '{print $1}' | xargs -r docker rm
```

### 3. Создать новый профиль через веб-интерфейс

### 4. Проверить права в новом контейнере:

```bash
# Проверить права на скрипт
docker exec maskbrowser-profile-<ID> ls -la /opt/bin/start-websockify.sh

# Должно показать:
# -rwxr-xr-x 1 root root ... /opt/bin/start-websockify.sh

# Проверить, что seluser может выполнить скрипт
docker exec maskbrowser-profile-<ID> su - seluser -c "/opt/bin/start-websockify.sh --help" || echo "Проверка выполнения"
```

### 5. Проверить статус supervisor:

```bash
docker exec maskbrowser-profile-<ID> supervisorctl status
```

**Ожидаемый результат:**
```
websockify      RUNNING
```

---

## 🔍 Что было исправлено:

1. **Dockerfile.browser:**
   - Убедились, что директория `/opt/bin` существует и имеет правильные права
   - Установили права `755` на скрипт и директорию
   - Добавили проверку прав после копирования

2. **websockify.conf:**
   - Изменили команду на `/bin/bash /opt/bin/start-websockify.sh` (явный вызов через bash)
   - Увеличили `startsecs` до 10 секунд
   - Увеличили `startretries` до 5
   - Добавили переменные окружения `PATH` и `HOME`
   - Установили `directory=/home/seluser`

3. **start-websockify.sh:**
   - Добавили экспорт `PATH` для гарантии доступа к командам

---

## 🚀 Команды для применения:

```bash
# 1. Пересобрать образ
cd /opt/mask-browser/infra
docker build --no-cache -t maskbrowser/browser:latest -f Dockerfile.browser .

# 2. Остановить старые контейнеры
docker ps | grep maskbrowser-profile | awk '{print $1}' | xargs -r docker stop
docker ps -a | grep maskbrowser-profile | awk '{print $1}' | xargs -r docker rm

# 3. Создать новый профиль через веб-интерфейс

# 4. Проверить логи
docker logs maskbrowser-profile-<NEW_ID> --tail 50 | grep websockify

# 5. Проверить статус
docker exec maskbrowser-profile-<NEW_ID> supervisorctl status websockify
```

---

## ✅ Ожидаемый результат:

После пересборки образа и создания нового профиля:

- ✅ websockify запускается через supervisor
- ✅ Нет ошибок `EACCES`
- ✅ websockify слушает на порту 6080
- ✅ noVNC доступен по `http://<IP>:<PORT>/vnc.html`

