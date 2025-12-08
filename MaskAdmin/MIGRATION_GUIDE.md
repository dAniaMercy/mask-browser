# MaskAdmin Database Migration Guide

## Миграция: Добавление колонок IsBanned и IsFrozen

### Дата создания
2025-12-08

### Описание
Эта миграция добавляет две новые колонки в таблицу `Users`:
- `IsBanned` (boolean, default: false) - флаг блокировки пользователя
- `IsFrozen` (boolean, default: false) - флаг заморозки аккаунта

Также создаются индексы на эти колонки для быстрого поиска заблокированных/замороженных пользователей.

### Имя миграции
`20251208193835_AddIsBannedAndIsFrozenColumns`

---

## Как применить миграцию

### Вариант 1: Автоматически при запуске приложения
Приложение автоматически применит миграцию при старте (если включена автомиграция в `Program.cs`).

### Вариант 2: Вручную через командную строку

#### 1. Убедитесь, что PostgreSQL запущена
```bash
# Проверьте подключение
psql -h localhost -p 5432 -U maskadmin -d maskbrowser
```

#### 2. Примените миграцию
```bash
cd d:\Proj\MaskBrowser_old\MaskAdmin
dotnet ef database update
```

#### 3. Проверьте результат
```sql
-- Подключитесь к БД
psql -h localhost -p 5432 -U maskadmin -d maskbrowser

-- Проверьте структуру таблицы Users
\d "Users"

-- Проверьте индексы
\di

-- Должны появиться:
-- IX_Users_IsBanned
-- IX_Users_IsFrozen
```

---

## Откат миграции (если нужно)

### Откатить последнюю миграцию
```bash
cd d:\Proj\MaskBrowser_old\MaskAdmin
dotnet ef database update 0
```

### Удалить файл миграции
```bash
dotnet ef migrations remove
```

---

## Проверка после миграции

### SQL запросы для проверки
```sql
-- Проверить, что колонки добавлены
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_name = 'Users'
AND column_name IN ('IsBanned', 'IsFrozen');

-- Проверить индексы
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'Users'
AND indexname LIKE '%Banned%' OR indexname LIKE '%Frozen%';

-- Проверить значения (все должны быть false)
SELECT "Username", "IsBanned", "IsFrozen" FROM "Users";
```

---

## Возможные проблемы и решения

### Ошибка: "Database does not exist"
```bash
# Создайте базу данных
psql -h localhost -p 5432 -U postgres
CREATE DATABASE maskbrowser;
CREATE USER maskadmin WITH PASSWORD 'SuperSecurePass!';
GRANT ALL PRIVILEGES ON DATABASE maskbrowser TO maskadmin;
```

### Ошибка: "Pending model changes"
Это нормально - мы специально отключили это предупреждение в `ApplicationDbContext.cs` (строка 20-21).

### Ошибка: "Could not connect to database"
Убедитесь, что:
1. PostgreSQL запущена
2. Порт 5432 доступен
3. Credentials в `appsettings.json` корректны
4. Firewall не блокирует подключение

---

## Что дальше?

После успешной миграции можно использовать новые поля:

### В коде C#
```csharp
// Заблокировать пользователя
user.IsBanned = true;
await _context.SaveChangesAsync();

// Заморозить аккаунт
user.IsFrozen = true;
await _context.SaveChangesAsync();

// Поиск заблокированных пользователей
var bannedUsers = await _context.Users
    .Where(u => u.IsBanned)
    .ToListAsync();
```

### В SQL
```sql
-- Заблокировать пользователя
UPDATE "Users" SET "IsBanned" = true WHERE "Id" = 5;

-- Разблокировать
UPDATE "Users" SET "IsBanned" = false WHERE "Id" = 5;

-- Найти всех заблокированных
SELECT * FROM "Users" WHERE "IsBanned" = true;

-- Найти замороженных
SELECT * FROM "Users" WHERE "IsFrozen" = true;
```

---

## История миграций

Чтобы посмотреть список всех применённых миграций:
```bash
dotnet ef migrations list
```

Чтобы посмотреть SQL, который будет выполнен:
```bash
dotnet ef migrations script
```
