-- Команда для создания admin пользователя в psql
-- Выполнить: psql -h localhost -U maskadmin -d maskbrowser -f create-admin-command.sql
-- Или в Docker: docker exec -i maskbrowser-postgres psql -U maskadmin -d maskbrowser < create-admin-command.sql

-- Сначала нужно сгенерировать BCrypt hash для пароля "Admin123!"
-- Можно использовать онлайн генератор или Python: bcrypt.hashpw(b'Admin123!', bcrypt.gensalt())

-- Вариант 1: Если пользователя нет - создаем
INSERT INTO "Users" (
    "Id",
    "Username",
    "Email",
    "PasswordHash",
    "Balance",
    "IsActive",
    "IsFrozen",
    "IsAdmin",
    "TwoFactorEnabled",
    "TwoFactorSecret",
    "CreatedAt"
)
SELECT 
    COALESCE(MAX("Id"), 0) + 1,
    'admin',
    'admin@maskbrowser.com',
    '$2a$11$rOzJqJqJqJqJqJqJqJqJqOuJqJqJqJqJqJqJqJqJqJqJqJqJqJqJqJqJqJqJq',  -- Нужно заменить на реальный BCrypt hash
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

-- Вариант 2: Обновляем существующего пользователя
UPDATE "Users"
SET 
    "IsActive" = true,
    "IsAdmin" = true,
    "PasswordHash" = '$2a$11$rOzJqJqJqJqJqJqJqJqJqOuJqJqJqJqJqJqJqJqJqJqJqJqJqJqJqJqJqJqJq',  -- Нужно заменить на реальный BCrypt hash
    "Balance" = 0,
    "IsFrozen" = false,
    "TwoFactorEnabled" = false
WHERE "Username" = 'admin' OR "Email" = 'admin@maskbrowser.com';

-- Проверка
SELECT "Id", "Username", "Email", "IsActive", "IsAdmin" FROM "Users" WHERE "Username" = 'admin';
