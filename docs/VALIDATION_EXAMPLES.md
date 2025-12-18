# Примеры валидации конфигурации профиля

## Обзор

Валидация конфигурации профиля автоматически выполняется при создании профиля через API. Валидатор проверяет формат и корректность всех полей конфигурации.

## Валидные конфигурации

### Пример 1: Стандартная конфигурация

```json
{
  "name": "My Profile",
  "config": {
    "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    "screenResolution": "1920x1080",
    "timezone": "UTC",
    "language": "en-US",
    "webRTC": false,
    "canvas": false,
    "webGL": false
  }
}
```

✅ **Результат:** Валидация пройдена

---

### Пример 2: Конфигурация с рекомендованными значениями

```json
{
  "name": "European Profile",
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

✅ **Результат:** Валидация пройдена (с предупреждением, если timezone/language не в списке рекомендованных)

---

## Невалидные конфигурации

### Пример 1: Неверный формат разрешения

```json
{
  "name": "Invalid Profile",
  "config": {
    "userAgent": "Mozilla/5.0...",
    "screenResolution": "1920x1080x32",
    "timezone": "UTC",
    "language": "en-US"
  }
}
```

❌ **Ошибка:** `Invalid screen resolution format. Expected format: WIDTHxHEIGHT (e.g., 1920x1080)`

---

### Пример 2: Пустой UserAgent

```json
{
  "name": "Empty UA Profile",
  "config": {
    "userAgent": "",
    "screenResolution": "1920x1080",
    "timezone": "UTC",
    "language": "en-US"
  }
}
```

❌ **Ошибка:** `UserAgent is required`

---

### Пример 3: Слишком длинный UserAgent

```json
{
  "name": "Long UA Profile",
  "config": {
    "userAgent": "Mozilla/5.0..." + "x".repeat(500),
    "screenResolution": "1920x1080",
    "timezone": "UTC",
    "language": "en-US"
  }
}
```

❌ **Ошибка:** `UserAgent is too long (max 500 characters)`

---

### Пример 4: Пустое разрешение

```json
{
  "name": "No Resolution",
  "config": {
    "userAgent": "Mozilla/5.0...",
    "screenResolution": "",
    "timezone": "UTC",
    "language": "en-US"
  }
}
```

❌ **Ошибка:** `ScreenResolution is required`

---

## Предупреждения (не блокируют создание)

### Пример 1: Нерекомендуемое разрешение

```json
{
  "name": "Custom Resolution",
  "config": {
    "userAgent": "Mozilla/5.0...",
    "screenResolution": "2560x1600",
    "timezone": "UTC",
    "language": "en-US"
  }
}
```

⚠️ **Предупреждение:** `ScreenResolution '2560x1600' is not in the recommended list. Recommended: 1920x1080, 1366x768, 1536x864, 1440x900, 1280x720`

✅ **Результат:** Профиль создан (с предупреждением)

---

### Пример 2: Нерекомендуемый timezone

```json
{
  "name": "Custom Timezone",
  "config": {
    "userAgent": "Mozilla/5.0...",
    "screenResolution": "1920x1080",
    "timezone": "America/New_York",
    "language": "en-US"
  }
}
```

⚠️ **Предупреждение:** `Timezone 'America/New_York' is not in the recommended list. Recommended: UTC, America/New_York, America/Los_Angeles, Europe/London, Europe/Moscow`

✅ **Результат:** Профиль создан (с предупреждением)

---

## Список разрешенных значений

### Разрешения экрана (рекомендованные)

- `1920x1080` (Full HD)
- `1366x768` (HD)
- `1536x864`
- `1440x900`
- `1280x720` (HD Ready)
- `1600x900`
- `1024x768`
- `1280x1024`
- `2560x1440` (2K)
- `3840x2160` (4K)

**Формат:** `WIDTHxHEIGHT`, где WIDTH и HEIGHT - числа от 100 до 9999

---

### Timezone (рекомендованные)

- `UTC`
- `America/New_York`
- `America/Los_Angeles`
- `Europe/London`
- `Europe/Moscow`
- `Asia/Tokyo`
- `Asia/Shanghai`
- `Australia/Sydney`
- `America/Chicago`
- `America/Denver`
- `Europe/Paris`
- `Europe/Berlin`

**Примечание:** Другие timezone также принимаются, но с предупреждением

---

### Языки (рекомендованные)

- `en-US` (English - United States)
- `en-GB` (English - United Kingdom)
- `ru-RU` (Russian)
- `de-DE` (German)
- `fr-FR` (French)
- `es-ES` (Spanish)
- `it-IT` (Italian)
- `pt-BR` (Portuguese - Brazil)
- `ja-JP` (Japanese)
- `zh-CN` (Chinese)
- `ko-KR` (Korean)

**Примечание:** Другие языки также принимаются, но с предупреждением

---

## Использование в коде

### C# (Server)

```csharp
var config = new BrowserConfig
{
    UserAgent = "Mozilla/5.0...",
    ScreenResolution = "1920x1080",
    Timezone = "UTC",
    Language = "en-US"
};

var validationResult = BrowserConfigValidator.Validate(config);
if (!validationResult.IsValid)
{
    // Обработка ошибок
    foreach (var error in validationResult.Errors)
    {
        Console.WriteLine($"Error: {error}");
    }
    return;
}

// Обработка предупреждений
foreach (var warning in validationResult.Warnings)
{
    Console.WriteLine($"Warning: {warning}");
}

// Конфигурация валидна, можно использовать
```

### JavaScript/TypeScript (Client)

```typescript
const config = {
  userAgent: "Mozilla/5.0...",
  screenResolution: "1920x1080",
  timezone: "UTC",
  language: "en-US"
};

// Валидация на клиенте (опционально, серверная валидация обязательна)
function validateConfig(config: BrowserConfig): ValidationResult {
  const errors: string[] = [];
  const warnings: string[] = [];

  // Проверка разрешения
  if (!/^\d{3,4}x\d{3,4}$/.test(config.screenResolution)) {
    errors.push("Invalid screen resolution format");
  }

  // Проверка UserAgent
  if (!config.userAgent || config.userAgent.length > 500) {
    errors.push("UserAgent is required and must be less than 500 characters");
  }

  return { isValid: errors.length === 0, errors, warnings };
}
```

---

## API Примеры

### Успешное создание профиля

```bash
curl -X POST "https://maskbrowser.ru/api/profile" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Profile",
    "config": {
      "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
      "screenResolution": "1920x1080",
      "timezone": "UTC",
      "language": "en-US",
      "webRTC": false,
      "canvas": false,
      "webGL": false
    }
  }'
```

**Ответ:** `200 OK` с данными профиля

---

### Ошибка валидации

```bash
curl -X POST "https://maskbrowser.ru/api/profile" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Invalid Profile",
    "config": {
      "userAgent": "Mozilla/5.0...",
      "screenResolution": "invalid",
      "timezone": "UTC",
      "language": "en-US"
    }
  }'
```

**Ответ:** `400 Bad Request`
```json
{
  "message": "Invalid profile configuration: Invalid screen resolution format. Expected format: WIDTHxHEIGHT (e.g., 1920x1080)"
}
```

---

## Получение конфигурации по умолчанию

```csharp
var defaultConfig = BrowserConfigValidator.GetDefaultConfig();
// Возвращает валидную конфигурацию по умолчанию
```

**Результат:**
```json
{
  "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
  "screenResolution": "1920x1080",
  "timezone": "UTC",
  "language": "en-US",
  "webRTC": false,
  "canvas": false,
  "webGL": false
}
```

---

## Лучшие практики

1. **Всегда используйте валидацию на сервере** - клиентская валидация опциональна
2. **Используйте рекомендованные значения** - они протестированы и работают лучше
3. **Обрабатывайте предупреждения** - они могут указывать на потенциальные проблемы
4. **Логируйте ошибки валидации** - для анализа и улучшения системы
5. **Используйте конфигурацию по умолчанию** - если пользователь не указал параметры

---

**Дата создания:** $(date)
**Версия:** 1.0
