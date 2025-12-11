# Конфигурация базы данных MaskAdmin

## Данные подключения PostgreSQL

```bash
POSTGRES_USER=maskuser
POSTGRES_PASSWORD=maskpass123
POSTGRES_DB=maskbrowser
POSTGRES_PORT=5432
```

## Строка подключения

### Development (appsettings.json)

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=maskbrowser;Username=maskuser;Password=maskpass123"
  }
}
```

### Production (appsettings.Production.json)

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=db;Port=5432;Database=maskbrowser;Username=maskuser;Password=maskpass123"
  }
}
```

### Docker Compose (переменные окружения)

```yaml
environment:
  - ConnectionStrings__PostgreSQL=Host=db;Port=5432;Database=maskbrowser;Username=maskuser;Password=maskpass123
```

## Команды psql

### Подключение к базе данных

```bash
# Локально
psql -h localhost -p 5432 -U maskuser -d maskbrowser

# В Docker контейнере
docker exec -it mask-browser-db psql -U maskuser -d maskbrowser
```

### Проверка администратора

```sql
-- Войти в psql
psql -U maskuser -d maskbrowser

-- Проверить администратора
SELECT "Id", "Username", "Email", "IsActive", "IsAdmin", "IsBanned", "IsFrozen"
FROM "Users"
WHERE "IsAdmin" = true;
```

### Создание администратора через SQL

```sql
-- Подключиться
psql -U maskuser -d maskbrowser

-- Сгенерируйте BCrypt хеш для пароля "Admin123!" через онлайн генератор или код
-- Пример хеша: $2a$11$example...

INSERT INTO "Users" (
    "Username",
    "Email",
    "PasswordHash",
    "Balance",
    "IsActive",
    "IsAdmin",
    "IsBanned",
    "IsFrozen",
    "TwoFactorEnabled",
    "CreatedAt"
)
VALUES (
    'admin',
    'admin@maskbrowser.com',
    '$2a$11$YourBCryptHashHere', -- Замените на реальный хеш
    0,
    true,
    true,
    false,
    false,
    false,
    NOW()
) ON CONFLICT ("Username") DO NOTHING;
```

### Разблокировка администратора

```sql
UPDATE "Users"
SET "IsActive" = true,
    "IsBanned" = false,
    "IsFrozen" = false
WHERE "Username" = 'admin';
```

### Сброс пароля через SQL

```sql
-- Сначала сгенерируйте новый BCrypt хеш для нового пароля
-- Например, для пароля "NewPassword123!": $2a$11$newHash...

UPDATE "Users"
SET "PasswordHash" = '$2a$11$YourNewHashHere'
WHERE "Username" = 'admin';
```

## Создание базы данных и пользователя

Если база данных еще не создана:

```bash
# Подключиться как postgres superuser
sudo -u postgres psql

# Создать пользователя
CREATE USER maskuser WITH PASSWORD 'maskpass123';

# Создать базу данных
CREATE DATABASE maskbrowser OWNER maskuser;

# Дать права
GRANT ALL PRIVILEGES ON DATABASE maskbrowser TO maskuser;

# Выйти
\q
```

## Применение миграций

### Локально

```bash
cd MaskAdmin
dotnet ef database update
```

### В Docker

```bash
# Если используется docker-compose
docker-compose exec maskadmin dotnet ef database update

# Или через docker run
docker exec -it maskadmin-container dotnet ef database update
```

## Проверка подключения

### Тест подключения через psql

```bash
psql -h localhost -U maskuser -d maskbrowser -c "SELECT version();"
```

Должен вывести версию PostgreSQL.

### Тест подключения через приложение

```bash
# Запустить приложение
cd MaskAdmin
dotnet run

# Проверить логи
# Должно быть: "Database connection successful" или подобное
```

## Backup и восстановление

### Создание backup

```bash
# Полный backup
pg_dump -U maskuser -d maskbrowser -F c -f maskbrowser_backup_$(date +%Y%m%d).dump

# Только схема
pg_dump -U maskuser -d maskbrowser -s -f maskbrowser_schema.sql

# Только данные
pg_dump -U maskuser -d maskbrowser -a -f maskbrowser_data.sql
```

### Восстановление из backup

```bash
# Из custom format backup
pg_restore -U maskuser -d maskbrowser -c maskbrowser_backup_20241208.dump

# Из SQL файла
psql -U maskuser -d maskbrowser < maskbrowser_backup.sql
```

## Переменные окружения

### Linux/Mac

```bash
export POSTGRES_USER=maskuser
export POSTGRES_PASSWORD=maskpass123
export POSTGRES_DB=maskbrowser
export ConnectionStrings__PostgreSQL="Host=localhost;Port=5432;Database=maskbrowser;Username=maskuser;Password=maskpass123"
```

### Windows (PowerShell)

```powershell
$env:POSTGRES_USER="maskuser"
$env:POSTGRES_PASSWORD="maskpass123"
$env:POSTGRES_DB="maskbrowser"
$env:ConnectionStrings__PostgreSQL="Host=localhost;Port=5432;Database=maskbrowser;Username=maskuser;Password=maskpass123"
```

### Windows (CMD)

```cmd
set POSTGRES_USER=maskuser
set POSTGRES_PASSWORD=maskpass123
set POSTGRES_DB=maskbrowser
set ConnectionStrings__PostgreSQL=Host=localhost;Port=5432;Database=maskbrowser;Username=maskuser;Password=maskpass123
```

## Обновление скриптов

Замените во всех скриптах:

```bash
# Старое
psql -U postgres -d mask_browser

# Новое
psql -U maskuser -d maskbrowser
```

### Файлы для обновления:

- `scripts/check-admin.sql`
- `scripts/create-admin.sh`
- `scripts/reset-password.sh`
- `scripts/README.md`
- `DEPLOYMENT.md`
- `TESTING_GUIDE.md`
- `AUTH_QUICK_START.md`
- `docs/AUTHENTICATION.md`

## Troubleshooting

### Ошибка: "password authentication failed for user"

```bash
# Проверить пароль
psql -U maskuser -d maskbrowser -W
# Введите: maskpass123

# Если не работает, проверьте pg_hba.conf
sudo nano /etc/postgresql/*/main/pg_hba.conf

# Должна быть строка:
# local   all   all   md5
# или
# host    all   all   127.0.0.1/32   md5
```

### Ошибка: "database does not exist"

```bash
# Создать базу данных
sudo -u postgres createdb -O maskuser maskbrowser

# Или через psql
sudo -u postgres psql -c "CREATE DATABASE maskbrowser OWNER maskuser;"
```

### Ошибка: "role does not exist"

```bash
# Создать пользователя
sudo -u postgres createuser -P maskuser
# Введите пароль: maskpass123

# Или через psql
sudo -u postgres psql -c "CREATE USER maskuser WITH PASSWORD 'maskpass123';"
```

## Безопасность

### В production:

1. **Используйте сильный пароль**
   ```bash
   POSTGRES_PASSWORD=$(openssl rand -base64 32)
   ```

2. **Ограничьте доступ в pg_hba.conf**
   ```
   # Только localhost
   host    maskbrowser    maskuser    127.0.0.1/32    md5

   # Или конкретный IP
   host    maskbrowser    maskuser    10.0.0.5/32     md5
   ```

3. **Используйте SSL**
   ```
   Host=localhost;Port=5432;Database=maskbrowser;Username=maskuser;Password=maskpass123;SSL Mode=Require
   ```

4. **Храните пароли в секретах**
   - Azure Key Vault
   - AWS Secrets Manager
   - HashiCorp Vault
   - Kubernetes Secrets

5. **Регулярно меняйте пароли**
   ```sql
   ALTER USER maskuser WITH PASSWORD 'newSecurePassword456!';
   ```

## Quick Commands

```bash
# Проверить подключение
psql -U maskuser -d maskbrowser -c "\conninfo"

# Список таблиц
psql -U maskuser -d maskbrowser -c "\dt"

# Размер базы данных
psql -U maskuser -d maskbrowser -c "SELECT pg_size_pretty(pg_database_size('maskbrowser'));"

# Количество пользователей
psql -U maskuser -d maskbrowser -c "SELECT COUNT(*) FROM \"Users\";"

# Количество администраторов
psql -U maskuser -d maskbrowser -c "SELECT COUNT(*) FROM \"Users\" WHERE \"IsAdmin\" = true;"

# Последние 5 пользователей
psql -U maskuser -d maskbrowser -c "SELECT \"Id\", \"Username\", \"Email\", \"CreatedAt\" FROM \"Users\" ORDER BY \"CreatedAt\" DESC LIMIT 5;"
```
