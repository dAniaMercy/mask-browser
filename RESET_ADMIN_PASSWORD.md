# Сброс пароля admin пользователя

## Команда для сброса пароля:

```bash
docker exec -i maskbrowser-postgres psql -U maskuser -d maskbrowser << 'EOF'
-- Сбросить пароль admin на "Admin123!"
-- BCrypt hash для "Admin123!" (сгенерирован через BCrypt.Net)
UPDATE "Users" 
SET 
    "PasswordHash" = '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    "IsActive" = true,
    "IsAdmin" = true
WHERE "Username" = 'admin' OR "Email" = 'admin@maskbrowser.com';

-- Проверить результат
SELECT "Id", "Username", "Email", "IsActive", "IsAdmin", 
       LEFT("PasswordHash", 20) as "PasswordHashPreview" 
FROM "Users" 
WHERE "Username" = 'admin';
EOF
```

## Альтернатива: сгенерировать новый hash

Если нужно сгенерировать новый BCrypt hash:

```bash
# В контейнере MaskAdmin (если есть Python)
docker exec maskbrowser-maskadmin python3 -c "import bcrypt; print(bcrypt.hashpw(b'Admin123!', bcrypt.gensalt()).decode())"

# Или использовать онлайн генератор BCrypt
```

## Данные для входа после сброса:

- **Username:** `admin`
- **Password:** `Admin123!`
