# Простая команда для создания admin пользователя

## Выполнить на сервере:

```bash
docker exec -i maskbrowser-postgres psql -U maskuser -d maskbrowser << 'EOF'
UPDATE "Users"
SET "IsActive" = true, "IsAdmin" = true, "PasswordHash" = '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', "Balance" = 0, "TwoFactorEnabled" = false
WHERE "Username" = 'admin' OR "Email" = 'admin@maskbrowser.com';

INSERT INTO "Users" ("Id", "Username", "Email", "PasswordHash", "Balance", "IsActive", "IsAdmin", "TwoFactorEnabled", "TwoFactorSecret", "CreatedAt")
SELECT COALESCE(MAX("Id"), 0) + 1, 'admin', 'admin@maskbrowser.com', '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 0, true, true, false, NULL, NOW()
FROM "Users"
WHERE NOT EXISTS (SELECT 1 FROM "Users" WHERE "Username" = 'admin' OR "Email" = 'admin@maskbrowser.com');

SELECT "Id", "Username", "Email", "IsActive", "IsAdmin" FROM "Users" WHERE "Username" = 'admin';
EOF
```

## Данные для входа:
- **Username:** `admin`
- **Password:** `Admin123!`
