# API Диагностики - Руководство по использованию

## Обзор

API диагностики предоставляет администраторам инструменты для мониторинга и управления системой MaskBrowser. Все endpoints требуют аутентификации и роли `Admin`.

## Аутентификация

Все запросы требуют JWT токен в заголовке:
```
Authorization: Bearer YOUR_JWT_TOKEN
```

## Endpoints

### 1. Получить список всех контейнеров

**GET** `/api/diagnostics/containers`

Возвращает список всех запущенных контейнеров с детальной информацией о каждом.

**Ответ:**
```json
{
  "totalContainers": 5,
  "containers": [
    {
      "containerId": "abc123...",
      "name": "/maskbrowser-profile-1",
      "status": "running",
      "health": {
        "isRunning": true,
        "healthStatus": "healthy",
        "uptime": 3600.5,
        "port": 15001
      },
      "profile": {
        "profileId": 1,
        "profileName": "My Profile",
        "userId": 10,
        "status": "Running"
      },
      "created": "2024-01-15T10:00:00Z",
      "image": "maskbrowser/browser:latest"
    }
  ]
}
```

**Пример использования:**
```bash
curl -X GET "https://maskbrowser.ru/api/diagnostics/containers" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### 2. Получить здоровье конкретного контейнера

**GET** `/api/diagnostics/containers/{containerId}/health`

Возвращает детальную информацию о здоровье контейнера.

**Параметры:**
- `containerId` (path) - ID контейнера

**Ответ:**
```json
{
  "containerId": "abc123...",
  "isHealthy": true,
  "status": "running",
  "healthStatus": "healthy",
  "isRunning": true,
  "startedAt": "2024-01-15T10:00:00Z",
  "uptime": {
    "totalSeconds": 3600.5,
    "totalMinutes": 60.0,
    "totalHours": 1.0,
    "days": 0
  },
  "port": 15001,
  "exitCode": null
}
```

**Пример использования:**
```bash
curl -X GET "https://maskbrowser.ru/api/diagnostics/containers/abc123/health" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### 3. Получить статус синхронизации профилей

**GET** `/api/diagnostics/profiles/sync-status`

Проверяет синхронизацию между статусами профилей в БД и реальным состоянием контейнеров.

**Ответ:**
```json
{
  "totalRunningProfiles": 5,
  "totalRunningContainers": 5,
  "outOfSyncProfiles": 1,
  "orphanedContainers": 0,
  "details": {
    "outOfSync": [
      {
        "profileId": 3,
        "profileName": "Profile 3",
        "containerId": "xyz789...",
        "status": "Running",
        "userId": 10
      }
    ],
    "orphaned": []
  }
}
```

**Поля:**
- `outOfSyncProfiles` - профили со статусом Running, но контейнер не запущен
- `orphanedContainers` - контейнеры без соответствующего профиля в БД

**Пример использования:**
```bash
curl -X GET "https://maskbrowser.ru/api/diagnostics/profiles/sync-status" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### 4. Исправить статус профиля

**POST** `/api/diagnostics/profiles/{profileId}/fix-status`

Автоматически исправляет статус профиля на основе реального состояния контейнера.

**Параметры:**
- `profileId` (path) - ID профиля

**Ответ:**
```json
{
  "message": "Profile status updated to Error",
  "profileId": 3,
  "previousStatus": "Running",
  "newStatus": "Error",
  "reason": "Container is not healthy"
}
```

**Пример использования:**
```bash
curl -X POST "https://maskbrowser.ru/api/diagnostics/profiles/3/fix-status" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## Коды ответов

- `200 OK` - Успешный запрос
- `401 Unauthorized` - Требуется аутентификация
- `403 Forbidden` - Недостаточно прав (требуется роль Admin)
- `404 Not Found` - Ресурс не найден
- `500 Internal Server Error` - Внутренняя ошибка сервера

---

## Примеры использования

### Проверка всех контейнеров и их здоровья

```bash
#!/bin/bash

API_URL="https://maskbrowser.ru"
TOKEN="YOUR_JWT_TOKEN"

# Получить список контейнеров
CONTAINERS=$(curl -s -X GET "$API_URL/api/diagnostics/containers" \
  -H "Authorization: Bearer $TOKEN")

# Проверить здоровье каждого контейнера
echo "$CONTAINERS" | jq -r '.containers[].containerId' | while read containerId; do
  HEALTH=$(curl -s -X GET "$API_URL/api/diagnostics/containers/$containerId/health" \
    -H "Authorization: Bearer $TOKEN")
  
  IS_HEALTHY=$(echo "$HEALTH" | jq -r '.isHealthy')
  if [ "$IS_HEALTHY" = "true" ]; then
    echo "✓ Container $containerId is healthy"
  else
    echo "✗ Container $containerId is unhealthy"
  fi
done
```

### Автоматическое исправление рассинхронизации

```bash
#!/bin/bash

API_URL="https://maskbrowser.ru"
TOKEN="YOUR_JWT_TOKEN"

# Получить статус синхронизации
SYNC_STATUS=$(curl -s -X GET "$API_URL/api/diagnostics/profiles/sync-status" \
  -H "Authorization: Bearer $TOKEN")

# Исправить статусы всех рассинхронизированных профилей
echo "$SYNC_STATUS" | jq -r '.details.outOfSync[].profileId' | while read profileId; do
  echo "Fixing profile $profileId..."
  curl -s -X POST "$API_URL/api/diagnostics/profiles/$profileId/fix-status" \
    -H "Authorization: Bearer $TOKEN"
done
```

### Мониторинг здоровья системы

```bash
#!/bin/bash

API_URL="https://maskbrowser.ru"
TOKEN="YOUR_JWT_TOKEN"

# Проверить статус синхронизации
SYNC_STATUS=$(curl -s -X GET "$API_URL/api/diagnostics/profiles/sync-status" \
  -H "Authorization: Bearer $TOKEN")

OUT_OF_SYNC=$(echo "$SYNC_STATUS" | jq -r '.outOfSyncProfiles')
ORPHANED=$(echo "$SYNC_STATUS" | jq -r '.orphanedContainers')

if [ "$OUT_OF_SYNC" -gt 0 ] || [ "$ORPHANED" -gt 0 ]; then
  echo "WARNING: System health issues detected!"
  echo "Out of sync profiles: $OUT_OF_SYNC"
  echo "Orphaned containers: $ORPHANED"
  # Отправить алерт (email, Slack, etc.)
else
  echo "System is healthy"
fi
```

---

## Интеграция с мониторингом

### Prometheus

Метрики доступны через стандартный endpoint Prometheus:
```
GET /metrics
```

Доступные метрики:
- `maskbrowser_containers_active` - количество активных контейнеров
- `maskbrowser_containers_unhealthy` - количество нездоровых контейнеров
- `maskbrowser_containers_orphaned` - количество сиротских контейнеров
- `maskbrowser_container_health_checks_total` - общее количество проверок здоровья
- `maskbrowser_container_creation_seconds` - время создания контейнера

### Grafana Dashboard

Пример запроса для Grafana:
```promql
# Количество нездоровых контейнеров
maskbrowser_containers_unhealthy

# Процент здоровых контейнеров
(maskbrowser_containers_active - maskbrowser_containers_unhealthy) / maskbrowser_containers_active * 100

# Время создания контейнера (p95)
histogram_quantile(0.95, maskbrowser_container_creation_seconds_bucket)
```

---

## Безопасность

⚠️ **Важно:**
- Все endpoints требуют роль `Admin`
- Не передавайте токены в логах или публичных местах
- Используйте HTTPS в production
- Регулярно обновляйте токены

---

## Troubleshooting

### Ошибка 401 Unauthorized
- Проверьте, что токен валиден
- Убедитесь, что токен не истек
- Проверьте формат заголовка: `Authorization: Bearer TOKEN`

### Ошибка 403 Forbidden
- Убедитесь, что пользователь имеет роль `Admin`
- Проверьте права доступа в базе данных

### Контейнер не найден
- Проверьте, что контейнер существует: `docker ps -a | grep containerId`
- Убедитесь, что используется правильный ID контейнера

---

**Дата создания:** $(date)
**Версия API:** v1
