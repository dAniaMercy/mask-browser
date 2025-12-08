# Руководство по тестированию MaskAdmin

Это руководство поможет вам протестировать все функции, реализованные в Stage 1 и дополнительных задачах.

## Подготовка к тестированию

### 1. Применение миграции базы данных

```bash
cd MaskAdmin
dotnet ef database update
```

**Что проверить:**
- Команда выполняется без ошибок
- В таблице `users` появились колонки `IsBanned` и `IsFrozen`

**SQL для проверки:**
```sql
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_name = 'users'
AND column_name IN ('IsBanned', 'IsFrozen');
```

**Ожидаемый результат:**
- Обе колонки имеют тип `boolean` и значение по умолчанию `false`

### 2. Запуск приложения

```bash
dotnet run
```

**URL:** https://localhost:7289 или http://localhost:5082

---

## Тестирование функционала

### 1. Аутентификация и Rate Limiting

#### Тест 1.1: Успешный вход
1. Откройте `/Auth/Login`
2. Введите корректные учетные данные:
   - Username: `admin`
   - Password: `Admin123!` (или ваш пароль администратора)
3. Нажмите "Sign In"

**Ожидаемый результат:**
- Перенаправление на `/Dashboard`
- В верхнем меню отображается имя пользователя и кнопка Logout

#### Тест 1.2: Rate Limiting при неудачных попытках входа
1. Откройте `/Auth/Login`
2. Введите неправильный пароль 5 раз подряд
3. На 6-й попытке попробуйте войти с правильным паролем

**Ожидаемый результат:**
- После 5 неудачных попыток появляется сообщение "Too many login attempts"
- IP блокируется на 15 минут
- В логах появляется запись о блокировке

**Проверка логов:**
```bash
# В консоли, где запущено приложение, найдите:
[Warning] Rate limit exceeded for IP: xxx.xxx.xxx.xxx
```

#### Тест 1.3: Защищенность паролей
1. Попробуйте изменить пароль пользователя на слабый (например, "12345678")

**Ожидаемый результат:**
- Система отклоняет пароль с сообщением об ошибке
- Требования: минимум 8 символов, заглавная буква, цифра, спецсимвол

---

### 2. Dashboard (Панель управления)

#### Тест 2.1: Отображение статистики
1. Откройте `/Dashboard`

**Что проверить:**
- 4 карточки со статистикой:
  - Total Users (общее количество пользователей)
  - Active Profiles (активные профили)
  - Total Revenue (общий доход)
  - Active Subscriptions (активные подписки)
- Значения обновляются корректно

#### Тест 2.2: Графики
**Проверьте наличие:**
- Графика регистраций пользователей (Users Registration)
- Графика доходов (Revenue)

**Ожидаемый результат:**
- Графики отображаются с данными за последние 7 дней
- Если нет данных, показываются пустые графики с осями

#### Тест 2.3: Таблицы
**Проверьте:**
- Recent Users (последние пользователи) - показывает 5 последних
- Active Profiles (активные профили) - показывает 5 последних запущенных

#### Тест 2.4: Auto-refresh
**Что проверить:**
- Подождите 30 секунд
- Dashboard должен автоматически обновиться
- В консоли браузера (F12) должно появиться сообщение: "Dashboard refreshed at HH:mm:ss"

---

### 3. Users Management (Управление пользователями)

#### Тест 3.1: Просмотр списка пользователей
1. Откройте `/Users`

**Что проверить:**
- Таблица пользователей с колонками: ID, Username, Email, Balance, Status, Subscription, Actions
- Пагинация (если пользователей больше 50)
- Счетчик: "Users (X total)"

#### Тест 3.2: Поиск пользователей
1. В поле "Search" введите имя или email существующего пользователя
2. Нажмите "Search"

**Ожидаемый результат:**
- Отображаются только пользователи, соответствующие запросу
- Поиск работает по полям: username, email, telegram_id

#### Тест 3.3: Фильтрация по статусу
1. Выберите фильтр "Status":
   - Active (активные)
   - Inactive (неактивные)
   - Banned (заблокированные)
2. Нажмите "Search"

**Ожидаемый результат:**
- Отображаются только пользователи с выбранным статусом
- Бейдж статуса соответствует фильтру

#### Тест 3.4: Сортировка
1. Выберите "Sort by":
   - Newest (по дате регистрации, новые первыми)
   - Oldest (старые первыми)
   - Balance (по балансу, убывание)
   - Username (по имени, A-Z)

**Ожидаемый результат:**
- Список пересортируется согласно выбранному критерию

#### Тест 3.5: Создание пользователя
1. Нажмите "Create User"
2. Заполните форму:
   - Username: `testuser`
   - Email: `test@example.com`
   - Password: `TestPassword123!`
   - Balance: `100`
   - Role: `User`
3. Нажмите "Create User"

**Ожидаемый результат:**
- Модальное окно закрывается
- Появляется сообщение об успехе: "User created successfully"
- Новый пользователь появляется в списке

**Проверка в базе данных:**
```sql
SELECT id, username, email, balance, is_active, is_banned
FROM users
WHERE username = 'testuser';
```

#### Тест 3.6: Просмотр деталей пользователя
1. Нажмите на иконку "глаз" (View) у любого пользователя
2. Откроется страница `/Users/Details/{id}`

**Что проверить:**
- **Sidebar с быстрыми действиями:**
  - Edit User (редактировать)
  - Adjust Balance (изменить баланс)
  - Manage Subscription (управление подпиской)
  - Reset Password (сбросить пароль)
  - Ban/Unban User
  - Freeze/Unfreeze User
  - View Activity Logs
  - Delete User

- **Вкладки:**
  - Overview (обзор)
  - Profiles (профили пользователя)
  - Payments (платежи)
  - Activity (активность)

- **Статистика:**
  - Total Profiles
  - Active Profiles
  - Total Spent
  - Last Login

#### Тест 3.7: Редактирование пользователя
1. На странице деталей нажмите "Edit User"
2. Измените email на `newemail@example.com`
3. Нажмите "Save Changes"

**Ожидаемый результат:**
- Модальное окно закрывается
- Появляется сообщение: "User updated successfully"
- Email обновлен на странице

**Проверка в audit logs:**
- Откройте `/Logs`
- Найдите запись с Action = "UserUpdated"
- OldValues должны содержать старый email
- NewValues должны содержать новый email

#### Тест 3.8: Изменение баланса
1. На странице деталей нажмите "Adjust Balance"
2. Выберите "Add" и введите `50`
3. Введите причину: "Test balance adjustment"
4. Нажмите "Update Balance"

**Ожидаемый результат:**
- Баланс увеличивается на 50
- Появляется сообщение: "Balance updated successfully"
- В audit logs появляется запись "BalanceAdjusted" с деталями

**Повторите с вычитанием:**
- Выберите "Subtract" и введите `25`

#### Тест 3.9: Блокировка пользователя (Ban)
1. На странице деталей нажмите "Ban User"
2. Введите причину: "Test ban"
3. Нажмите "Ban User"

**Ожидаемый результат:**
- Статус пользователя меняется на "Banned" (красный бейдж)
- Кнопка меняется на "Unban User"
- is_active становится false
- is_banned становится true

**Проверка в БД:**
```sql
SELECT is_active, is_banned
FROM users
WHERE username = 'testuser';
```

**Разблокировка:**
1. Нажмите "Unban User"
2. Подтвердите

**Ожидаемый результат:**
- is_banned = false
- is_active = true

#### Тест 3.10: Заморозка пользователя (Freeze)
1. Нажмите "Freeze User"
2. Введите причину: "Test freeze"

**Ожидаемый результат:**
- Статус меняется на "Frozen" (желтый бейдж)
- is_frozen = true

#### Тест 3.11: Сброс пароля
1. Нажмите "Reset Password"
2. Введите новый пароль: `NewPassword123!`
3. Нажмите "Reset Password"

**Ожидаемый результат:**
- Сообщение об успехе
- В audit logs запись "PasswordReset"

**Проверка:**
- Выйдите из системы
- Попробуйте войти как `testuser` со старым паролем (должно не получиться)
- Войдите с новым паролем (должно работать)

#### Тест 3.12: Удаление пользователя
1. В списке пользователей нажмите кнопку "Delete" (иконка корзины)
2. Подтвердите удаление

**Ожидаемый результат:**
- Пользователь удаляется из списка
- Сообщение: "User deleted successfully"

**Важно:** Удаляются только пользователи без активных подписок и профилей

---

### 4. Browser Profiles (Управление профилями)

#### Тест 4.1: Просмотр списка профилей
1. Откройте `/Profiles`

**Что проверить:**
- Таблица с колонками: ID, Name, User, Server, Status, Container ID, Port, Created, Runtime, Starts, Actions
- Счетчик профилей
- Пагинация

#### Тест 4.2: Фильтрация профилей
1. Используйте фильтры:
   - **User ID**: введите ID существующего пользователя
   - **Status**: выберите статус (Stopped, Starting, Running, Stopping, Error)
   - **Server ID**: введите ID сервера
2. Нажмите "Apply Filters"

**Ожидаемый результат:**
- Список фильтруется согласно параметрам
- URL содержит параметры фильтров

#### Тест 4.3: Запуск профиля (Start)
**Предусловия:**
- Профиль должен быть в статусе "Stopped" или "Error"
- Server node должен быть доступен и настроен в appsettings.json

1. Найдите профиль в статусе "Stopped"
2. Нажмите зеленую кнопку "Play" (Start)

**Ожидаемый результат:**
- Статус меняется на "Starting" (синий бейдж)
- Через несколько секунд меняется на "Running" (зеленый бейдж)
- Счетчик "Starts" увеличивается на 1
- LastStartedAt обновляется

**Проверка в БД:**
```sql
SELECT id, name, status, start_count, last_started_at
FROM browser_profiles
WHERE id = {profile_id};
```

**Проверка API запроса:**
- В логах приложения должна быть запись:
```
[Information] Starting profile {ProfileId}
[Information] Profile {ProfileId} started successfully
```

#### Тест 4.4: Остановка профиля (Stop)
1. Найдите профиль в статусе "Running"
2. Нажмите красную кнопку "Stop"

**Ожидаемый результат:**
- Статус меняется на "Stopping" (желтый бейдж)
- Через несколько секунд меняется на "Stopped" (серый бейдж)
- TotalRunTime увеличивается на время работы профиля

**Проверка Runtime:**
```sql
SELECT total_run_time
FROM browser_profiles
WHERE id = {profile_id};
```

#### Тест 4.5: Перезапуск профиля (Restart)
1. Найдите профиль в статусе "Running"
2. Нажмите оранжевую кнопку "Restart" (стрелка по кругу)

**Ожидаемый результат:**
- Профиль сначала останавливается
- Через 2 секунды запускается снова
- Сообщение: "Profile {id} restarted successfully"

#### Тест 4.6: Удаление профиля (Delete)
**Предусловие:** Профиль должен быть остановлен

1. Нажмите красную кнопку "Delete" (корзина)
2. Подтвердите удаление в диалоге

**Ожидаемый результат:**
- Профиль удаляется из списка
- Сообщение: "Profile {id} deleted successfully"

---

### 5. Audit Logs (Журнал аудита)

#### Тест 5.1: Просмотр логов
1. Откройте `/Logs`

**Что проверить:**
- Таблица с логами
- Колонки: Timestamp, Level, Category, User, Action, Entity, Entity ID, Details, IP Address
- Цветовая кодировка строк:
  - Error → красный фон
  - Critical → черный фон, белый текст
  - Warning → желтый фон

#### Тест 5.2: Фильтрация логов
1. Используйте фильтры:
   - **Search**: введите "UserUpdated" или имя пользователя
   - **Category**: выберите "UserManagement"
   - **Level**: выберите "Info"
   - **Date From**: выберите сегодняшнюю дату
   - **Date To**: выберите сегодняшнюю дату
2. Нажмите "Apply"

**Ожидаемый результат:**
- Отображаются только логи, соответствующие фильтрам
- Счетчик обновляется

#### Тест 5.3: Просмотр деталей лога
1. Найдите лог с кнопкой "View" в колонке "Details"
2. Нажмите "View"

**Ожидаемый результат:**
- Открывается модальное окно "Log Details"
- Отображаются секции (если есть данные):
  - Old Values (красный заголовок) - старые значения
  - New Values (зеленый заголовок) - новые значения
  - Additional Data (синий заголовок) - дополнительные данные
- JSON форматируется с отступами

#### Тест 5.4: Экспорт логов в CSV
1. Настройте фильтры (опционально)
2. Нажмите "Export CSV"

**Ожидаемый результат:**
- Скачивается файл `AuditLogs_YYYYMMDD_HHmmss.csv`
- Файл содержит все отфильтрованные логи
- Колонки: Timestamp, Level, Category, User, Action, Entity, EntityId, IpAddress, UserAgent, OldValues, NewValues, AdditionalData

#### Тест 5.5: Экспорт логов в Excel
1. Нажмите "Export Excel"

**Ожидаемый результат:**
- Скачивается файл `AuditLogs_YYYYMMDD_HHmmss.xlsx`
- Файл открывается в Excel/LibreOffice
- Содержит лист "AuditLogs" с данными
- Форматирование сохранено

---

### 6. Payment Webhooks (Вебхуки платежей)

#### Тест 6.1: CryptoBot webhook

**Подготовка:**
1. Убедитесь, что в `appsettings.json` указан `CryptoBot:WebhookSecret`
2. Создайте тестового пользователя с ID = 1

**Отправка webhook:**
```bash
curl -X POST https://localhost:7289/api/webhook/cryptobot \
  -H "Content-Type: application/json" \
  -H "X-Crypto-Bot-Signature: {вычисленная_подпись}" \
  -d '{
    "UpdateId": 123456,
    "Payload": {
      "PaymentId": 789012,
      "Amount": 10.50,
      "Asset": "USDT",
      "Payload": "1"
    }
  }'
```

**Вычисление подписи (C#):**
```csharp
var payload = "{\"UpdateId\":123456,\"Payload\":{\"PaymentId\":789012,\"Amount\":10.5,\"Asset\":\"USDT\",\"Payload\":\"1\"}}";
var secret = "your_webhook_secret";
using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
var signature = Convert.ToHexString(hash).ToLower();
```

**Ожидаемый результат:**
- HTTP 200 OK
- Ответ:
```json
{
  "status": "success",
  "paymentId": 123,
  "userId": 1,
  "amount": 10.50,
  "newBalance": 110.50
}
```

**Проверки:**
1. Баланс пользователя увеличился на 10.50
```sql
SELECT balance FROM users WHERE id = 1;
```

2. Создана запись в таблице payments:
```sql
SELECT * FROM payments WHERE transaction_id = '789012';
```
- Status = Completed
- Provider = CryptoBot
- Amount = 10.50
- Currency = USDT

3. Создан audit log:
```sql
SELECT * FROM audit_logs
WHERE action = 'PaymentReceived'
AND entity_id = {payment_id};
```

**Тест дубликата:**
- Отправьте тот же webhook повторно
- Ожидаемый результат: HTTP 200, `{"status": "already_processed"}`

**Тест неверной подписи:**
- Отправьте webhook с неправильной подписью
- Ожидаемый результат: HTTP 401, `{"error": "Invalid signature"}`

#### Тест 6.2: Bybit webhook

**Отправка webhook:**
```bash
curl -X POST https://localhost:7289/api/webhook/bybit \
  -H "Content-Type: application/json" \
  -H "X-Bybit-Signature: {Base64_подпись}" \
  -d '{
    "OrderId": "ORD123456",
    "Amount": 25.00,
    "Currency": "USDT",
    "Status": "SUCCESS",
    "CustomUserId": "1"
  }'
```

**Вычисление подписи (C#):**
```csharp
var payload = "{\"OrderId\":\"ORD123456\",\"Amount\":25.0,\"Currency\":\"USDT\",\"Status\":\"SUCCESS\",\"CustomUserId\":\"1\"}";
var secret = "your_webhook_secret";
using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
var signature = Convert.ToBase64String(hash);
```

**Ожидаемый результат:**
- HTTP 200 OK
- Баланс пользователя увеличился на 25.00
- Запись в payments с Provider = Bybit
- Audit log создан

**Тест статуса PENDING:**
- Отправьте webhook со Status = "PENDING"
- Ожидаемый результат:
  - Payment создан со Status = Pending
  - Баланс НЕ изменился
  - Audit log НЕ создан

**Тест статуса FAILED:**
- Отправьте webhook со Status = "FAILED" и FailureReason = "Insufficient funds"
- Ожидаемый результат:
  - Payment создан со Status = Failed
  - FailureReason заполнен
  - Баланс НЕ изменился

---

## Тестирование безопасности

### 1. HTTPS Redirect
**В production режиме:**
```bash
dotnet run --environment Production
```

1. Попробуйте открыть `http://localhost:5082`

**Ожидаемый результат:**
- Автоматический редирект на `https://localhost:7289`

### 2. Secure Cookies
1. Откройте DevTools (F12) → Application → Cookies
2. Проверьте cookie `.AspNetCore.Cookies`

**Ожидаемый результат:**
- HttpOnly = true
- Secure = true (в production)
- SameSite = Strict

### 3. Anti-CSRF Tokens
1. Откройте любую форму (например, Login)
2. Проверьте наличие скрытого поля `__RequestVerificationToken`

**Попытка отправить форму без токена:**
```bash
curl -X POST https://localhost:7289/Profiles/Start/1
```

**Ожидаемый результ: HTTP 400 Bad Request (Missing anti-forgery token)

---

## Проверка производительности

### 1. Dashboard Auto-refresh
1. Откройте Dashboard
2. Откройте DevTools → Network
3. Подождите 30 секунд

**Ожидаемый результат:**
- Каждые 30 секунд выполняется GET запрос к `/Dashboard`
- Страница обновляется без полной перезагрузки

### 2. Пагинация
1. Создайте 100+ пользователей (можно через SQL)
2. Откройте `/Users`

**Ожидаемый результат:**
- Отображается только 50 пользователей
- Пагинация работает корректно
- Переход между страницами быстрый (< 1 сек)

---

## Troubleshooting

### Ошибка: "Connection refused" при запуске профиля
**Решение:**
- Проверьте настройку `Server:ApiBaseUrl` в appsettings.json
- Убедитесь, что Server node запущен и доступен

### Ошибка: "Rate limit exceeded" даже после ожидания
**Решение:**
- Перезапустите приложение (in-memory хранилище очистится)
- Или подождите 15 минут

### Webhook возвращает "Invalid signature"
**Решение:**
- Проверьте, что `WebhookSecret` в конфигурации совпадает с используемым для вычисления подписи
- Убедитесь, что JSON payload сериализуется точно так же, как в коде

### Audit logs не записываются
**Решение:**
- Проверьте, что `DbContext.SaveChangesAsync()` вызывается после добавления AuditLog
- Проверьте логи приложения на наличие ошибок EF Core

---

## Заключение

Все основные функции реализованы и готовы к тестированию. Если возникнут проблемы:
1. Проверьте логи приложения (консоль)
2. Проверьте логи базы данных (audit_logs)
3. Используйте DevTools браузера для отладки frontend
4. Проверьте, что все миграции применены

Для дальнейшей разработки см. файл `IMPROVEMENTS.md` (Этап 2 и 3).
