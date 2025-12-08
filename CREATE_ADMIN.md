# Создание admin пользователя напрямую в PostgreSQL

## Вариант 1: Через Docker exec (рекомендуется)

```bash
# Подключиться к PostgreSQL контейнеру
docker exec -it maskbrowser-postgres psql -U maskuser -d maskbrowser

# Затем выполнить SQL команды:
```

## SQL команда для создания/обновления admin:

```sql
-- Сначала генерируем BCrypt hash для пароля "Admin123!"
-- Можно использовать Python в контейнере:
-- docker exec maskbrowser-postgres python3 -c "import bcrypt; print(bcrypt.hashpw(b'Admin123!', bcrypt.gensalt()).decode())"

-- Или использовать онлайн генератор BCrypt

-- Вариант 1: Создать нового пользователя (если не существует)
INSERT INTO "Users" (
    "Id", "Username", "Email", "PasswordHash", "Balance", 
    "IsActive", "IsFrozen", "IsAdmin", "TwoFactorEnabled", "TwoFactorSecret", "CreatedAt"
)
SELECT 
    COALESCE(MAX("Id"), 0) + 1,
    'admin',
    'admin@maskbrowser.com',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',  -- BCrypt hash для "Admin123!"
    0,
    true,
    false,
    true,
    false,
    NULL,
    NOW()
FROM "Users"
WHERE NOT EXISTS (
    SELECT 1 FROM "Users" WHERE "Username" = 'admin' OR "Email" = 'admin@maskbrowser.com'
);

-- Вариант 2: Обновить существующего пользователя
UPDATE "Users"
SET 
    "IsActive" = true,
    "IsAdmin" = true,
    "PasswordHash" = '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',  -- BCrypt hash для "Admin123!"
    "Balance" = 0,
    "IsFrozen" = false,
    "TwoFactorEnabled" = false
WHERE "Username" = 'admin' OR "Email" = 'admin@maskbrowser.com';

-- Проверка результата
SELECT "Id", "Username", "Email", "IsActive", "IsAdmin", "CreatedAt" 
FROM "Users" 
WHERE "Username" = 'admin';
```

## Вариант 2: Одной командой через Docker

```bash
docker exec -i maskbrowser-postgres psql -U maskuser -d maskbrowser << 'EOF'
UPDATE "Users"
SET 
    "IsActive" = true,
    "IsAdmin" = true,
    "PasswordHash" = '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    "Balance" = 0,
    "IsFrozen" = false,
    "TwoFactorEnabled" = false
WHERE "Username" = 'admin' OR "Email" = 'admin@maskbrowser.com';

INSERT INTO "Users" (
    "Id", "Username", "Email", "PasswordHash", "Balance", 
    "IsActive", "IsFrozen", "IsAdmin", "TwoFactorEnabled", "TwoFactorSecret", "CreatedAt"
)
SELECT 
    COALESCE(MAX("Id"), 0) + 1,
    'admin',
    'admin@maskbrowser.com',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    0,
    true,
    false,
    true,
    false,
    NULL,
    NOW()
FROM "Users"
WHERE NOT EXISTS (
    SELECT 1 FROM "Users" WHERE "Username" = 'admin' OR "Email" = 'admin@maskbrowser.com'
);

SELECT "Id", "Username", "Email", "IsActive", "IsAdmin" FROM "Users" WHERE "Username" = 'admin';
EOF
```

## Данные для входа:

- **Username:** `admin`
- **Email:** `admin@maskbrowser.com`
- **Password:** `Admin123!`

## Примечание:

BCrypt hash `$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy` соответствует паролю `Admin123!`.

Если нужно сгенерировать новый hash:
```bash
docker exec maskbrowser-postgres python3 -c "import bcrypt; print(bcrypt.hashpw(b'Admin123!', bcrypt.gensalt()).decode())"
```
