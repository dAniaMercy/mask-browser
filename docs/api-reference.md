# API Reference - MASK BROWSER

## Base URL

```
http://localhost:5050/api
```

## Аутентификация

Большинство эндпоинтов требуют JWT токен в заголовке:

```
Authorization: Bearer <token>
```

## Endpoints

### Auth

#### POST /auth/register

Регистрация нового пользователя.

**Request:**
```json
{
  "username": "user123",
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": 1,
    "username": "user123",
    "email": "user@example.com"
  }
}
```

#### POST /auth/login

Вход в систему.

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": 1,
    "username": "user123",
    "email": "user@example.com",
    "isAdmin": false
  }
}
```

#### GET /auth/me

Получить информацию о текущем пользователе.

**Headers:**
- `Authorization: Bearer <token>`

**Response:**
```json
{
  "userId": 1
}
```

### Profiles

#### GET /profile

Получить список профилей текущего пользователя.

**Headers:**
- `Authorization: Bearer <token>`

**Response:**
```json
[
  {
    "id": 1,
    "userId": 1,
    "name": "Profile 1",
    "containerId": "abc123",
    "serverNodeIp": "192.168.1.101",
    "port": 12345,
    "config": {
      "userAgent": "Mozilla/5.0...",
      "screenResolution": "1920x1080",
      "timezone": "UTC",
      "language": "en-US",
      "webRTC": false,
      "canvas": false,
      "webGL": false
    },
    "status": "Running",
    "createdAt": "2024-01-01T00:00:00Z",
    "lastStartedAt": "2024-01-01T12:00:00Z"
  }
]
```

#### GET /profile/{id}

Получить информацию о конкретном профиле.

**Headers:**
- `Authorization: Bearer <token>`

**Path Parameters:**
- `id` (int) - ID профиля

**Response:**
```json
{
  "id": 1,
  "userId": 1,
  "name": "Profile 1",
  "containerId": "abc123",
  "serverNodeIp": "192.168.1.101",
  "port": 12345,
  "config": {
    "userAgent": "Mozilla/5.0...",
    "screenResolution": "1920x1080",
    "timezone": "UTC",
    "language": "en-US",
    "webRTC": false,
    "canvas": false,
    "webGL": false
  },
  "status": "Running",
  "createdAt": "2024-01-01T00:00:00Z",
  "lastStartedAt": "2024-01-01T12:00:00Z"
}
```

#### POST /profile

Создать новый профиль.

**Headers:**
- `Authorization: Bearer <token>`

**Request:**
```json
{
  "name": "My Profile",
  "config": {
    "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
    "screenResolution": "1920x1080",
    "timezone": "Europe/Moscow",
    "language": "ru-RU",
    "webRTC": false,
    "canvas": false,
    "webGL": false
  }
}
```

**Response:**
```json
{
  "id": 2,
  "userId": 1,
  "name": "My Profile",
  "containerId": "",
  "serverNodeIp": "",
  "port": 0,
  "config": {
    "userAgent": "Mozilla/5.0...",
    "screenResolution": "1920x1080",
    "timezone": "Europe/Moscow",
    "language": "ru-RU",
    "webRTC": false,
    "canvas": false,
    "webGL": false
  },
  "status": "Stopped",
  "createdAt": "2024-01-01T13:00:00Z",
  "lastStartedAt": null
}
```

#### POST /profile/{id}/start

Запустить профиль.

**Headers:**
- `Authorization: Bearer <token>`

**Path Parameters:**
- `id` (int) - ID профиля

**Response:**
```json
{
  "message": "Profile started"
}
```

#### POST /profile/{id}/stop

Остановить профиль.

**Headers:**
- `Authorization: Bearer <token>`

**Path Parameters:**
- `id` (int) - ID профиля

**Response:**
```json
{
  "message": "Profile stopped"
}
```

#### DELETE /profile/{id}

Удалить профиль.

**Headers:**
- `Authorization: Bearer <token>`

**Path Parameters:**
- `id` (int) - ID профиля

**Response:**
```json
{
  "message": "Profile deleted"
}
```

### Server Management

#### POST /server/register

Зарегистрировать новую серверную ноду.

**Request:**
```json
{
  "name": "node-1",
  "ipAddress": "192.168.1.101",
  "maxContainers": 1000
}
```

**Response:**
```json
{
  "message": "Node registered"
}
```

#### POST /server/health

Обновить статус здоровья ноды.

**Request:**
```json
{
  "ipAddress": "192.168.1.101",
  "isHealthy": true,
  "cpuUsage": 45.5,
  "memoryUsage": 62.3
}
```

**Response:**
```json
{
  "message": "Health updated"
}
```

### Admin (Admin role required)

#### GET /admin/users

Получить список всех пользователей.

**Headers:**
- `Authorization: Bearer <admin_token>`

**Response:**
```json
{
  "message": "User list"
}
```

#### GET /admin/servers

Получить список всех серверов.

**Headers:**
- `Authorization: Bearer <admin_token>`

**Response:**
```json
{
  "message": "Server list"
}
```

#### GET /admin/payments

Получить список всех платежей.

**Headers:**
- `Authorization: Bearer <admin_token>`

**Response:**
```json
{
  "message": "Payment list"
}
```

## Metrics

### GET /metrics

Prometheus метрики (не требует аутентификации).

**Response:**
```
# HELP maskbrowser_containers_active Number of active containers
# TYPE maskbrowser_containers_active gauge
maskbrowser_containers_active 1250

# HELP maskbrowser_containers_created_total Total containers created
# TYPE maskbrowser_containers_created_total counter
maskbrowser_containers_created_total 5432

# HELP maskbrowser_containers_stopped_total Total containers stopped
# TYPE maskbrowser_containers_stopped_total counter
maskbrowser_containers_stopped_total 4182
```

## Status Codes

- `200 OK` - Успешный запрос
- `400 Bad Request` - Неверный запрос
- `401 Unauthorized` - Требуется аутентификация
- `403 Forbidden` - Недостаточно прав
- `404 Not Found` - Ресурс не найден
- `500 Internal Server Error` - Внутренняя ошибка сервера

## Error Response Format

```json
{
  "message": "Error description"
}
```

## Rate Limiting

- API endpoints: 100 запросов в минуту на IP
- Auth endpoints: 10 запросов в минуту на IP
- Используется Redis для rate limiting

