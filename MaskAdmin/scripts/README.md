# MaskAdmin Management Scripts

Набор скриптов для управления MaskAdmin: развертывание, обновление, откат и управление администратором.

## Скрипты развертывания

### deploy.sh - Автоматическое развертывание
Обновляет код из GitHub и перезапускает приложение (systemd).

```bash
# Сделать исполняемым
chmod +x deploy.sh

# Запустить развертывание
sudo ./deploy.sh

# Развернуть определенную ветку
sudo GIT_BRANCH=develop ./deploy.sh
```

**Что делает:**
1. Создает backup текущей версии
2. Останавливает службу
3. Обновляет код из GitHub
4. Применяет миграции БД
5. Собирает и публикует проект
6. Запускает службу
7. Выполняет health check

### docker-deploy.sh - Docker развертывание
Обновляет и перезапускает контейнер Docker.

```bash
chmod +x docker-deploy.sh
sudo ./docker-deploy.sh
```

**Использует:**
- Docker Compose из `/opt/mask-browser/infra`
- Пересобирает образ с флагом `--no-cache`
- Применяет миграции внутри контейнера

### rollback.sh - Откат к предыдущей версии
Восстанавливает приложение из backup.

```bash
chmod +x rollback.sh

# Интерактивный выбор backup
sudo ./rollback.sh

# Откат к конкретному backup (номер из списка)
sudo ./rollback.sh 2
```

### check-updates.sh - Проверка обновлений
Проверяет наличие новых коммитов в GitHub без применения.

```bash
chmod +x check-updates.sh
./check-updates.sh
```

## Автоматизация обновлений

### Cron задача для автоматического обновления

```bash
# Редактировать crontab
sudo crontab -e

# Обновлять каждую ночь в 3:00
0 3 * * * /opt/mask-browser/MaskAdmin/scripts/deploy.sh >> /var/log/maskadmin-deploy.log 2>&1

# Проверять обновления каждый час
0 * * * * /opt/mask-browser/MaskAdmin/scripts/check-updates.sh >> /var/log/maskadmin-updates.log 2>&1
```

### Webhook для автоматического развертывания

Создайте endpoint для GitHub webhook:

```bash
# Установите webhook listener
sudo apt install webhook

# Создайте конфигурацию /etc/webhook.conf
cat > /etc/webhook.conf <<'EOF'
[
  {
    "id": "maskadmin-deploy",
    "execute-command": "/opt/mask-browser/MaskAdmin/scripts/deploy.sh",
    "command-working-directory": "/opt/mask-browser/MaskAdmin",
    "response-message": "Deployment started",
    "trigger-rule": {
      "match": {
        "type": "payload-hash-sha256",
        "secret": "your-webhook-secret",
        "parameter": {
          "source": "header",
          "name": "X-Hub-Signature-256"
        }
      }
    }
  }
]
EOF

# Запустите webhook service
sudo systemctl enable webhook
sudo systemctl start webhook
```

Добавьте webhook в настройках GitHub:
- URL: `http://your-server:9000/hooks/maskadmin-deploy`
- Content type: `application/json`
- Secret: `your-webhook-secret`
- Events: `push`

## Скрипты управления администратором

## Проверка существующего администратора

### SQL скрипт

```bash
# На сервере с PostgreSQL
psql -U postgres -d mask_browser -f check-admin.sql
```

Или напрямую:
```bash
psql -U postgres -d mask_browser -c "SELECT \"Id\", \"Username\", \"Email\", \"IsActive\", \"IsAdmin\" FROM \"Users\" WHERE \"IsAdmin\" = true;"
```

## Создание администратора

### Способ 1: Через API endpoint (рекомендуется)

```bash
# Сделать скрипт исполняемым
chmod +x create-admin.sh

# Создать админа с паролем по умолчанию (Admin123!)
./create-admin.sh

# Создать админа с кастомным паролем
./create-admin.sh "MySecurePassword123!"

# Указать другой URL сервера
./create-admin.sh "Admin123!" "http://192.168.1.100:5000"
```

### Способ 2: Через curl напрямую

```bash
curl -X POST http://localhost:5000/create-admin \
  -H "Content-Type: application/json" \
  -d '{"password": "Admin123!"}'
```

### Способ 3: Через SQL

1. Сгенерируйте BCrypt хеш для вашего пароля:
   - Онлайн: https://bcrypt-generator.com/ (используйте rounds=11)
   - Или используйте C# код в приложении

2. Выполните SQL:
```sql
INSERT INTO "Users" (
    "Username",
    "Email",
    "PasswordHash",
    "Balance",
    "IsActive",
    "IsAdmin",
    "TwoFactorEnabled",
    "IsBanned",
    "IsFrozen",
    "CreatedAt"
)
VALUES (
    'admin',
    'admin@maskbrowser.com',
    '$2a$11$YourBCryptHashHere',
    0,
    true,
    true,
    false,
    false,
    false,
    NOW()
);
```

## Сброс пароля администратора

### Способ 1: Через API endpoint (рекомендуется)

```bash
# Сделать скрипт исполняемым
chmod +x reset-password.sh

# Сбросить пароль на значение по умолчанию (Admin123!)
./reset-password.sh

# Сбросить на кастомный пароль
./reset-password.sh "NewSecurePassword123!"

# Указать другой URL сервера
./reset-password.sh "Admin123!" "http://192.168.1.100:5000"
```

### Способ 2: Через curl напрямую

```bash
curl -X POST http://localhost:5000/reset-admin-password \
  -H "Content-Type: application/json" \
  -d '{"newPassword": "NewPassword123!"}'
```

### Способ 3: Через SQL

1. Сгенерируйте BCrypt хеш для нового пароля
2. Выполните SQL:
```sql
UPDATE "Users"
SET "PasswordHash" = '$2a$11$YourNewBCryptHashHere'
WHERE "Username" = 'admin' AND "IsAdmin" = true;
```

## Тестирование входа

```bash
# Проверка логина через API
curl -X POST http://localhost:5000/Auth/Login \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=admin&password=Admin123!"
```

Успешный ответ вернет cookie с JWT токеном и редирект на Dashboard.

## Troubleshooting

### Ошибка: "Invalid username or password"

**Причина 1: Администратор не создан**
```bash
# Проверьте наличие администратора
psql -U postgres -d mask_browser -c "SELECT COUNT(*) FROM \"Users\" WHERE \"IsAdmin\" = true;"
```
Если результат = 0, создайте администратора.

**Причина 2: Неправильный пароль**
- Попробуйте сбросить пароль через `reset-password.sh`
- Убедитесь, что не добавляете лишние пробелы в пароль

**Причина 3: Аккаунт заблокирован (IsBanned = true)**
```bash
# Проверьте статус
psql -U postgres -d mask_browser -c "SELECT \"Username\", \"IsBanned\", \"IsFrozen\", \"IsActive\" FROM \"Users\" WHERE \"Username\" = 'admin';"

# Разблокируйте если нужно
psql -U postgres -d mask_browser -c "UPDATE \"Users\" SET \"IsBanned\" = false, \"IsActive\" = true WHERE \"Username\" = 'admin';"
```

**Причина 4: Rate Limiting**
Если вы ввели неправильный пароль 5 раз, ваш IP заблокирован на 15 минут.
- Подождите 15 минут
- Или перезапустите приложение (in-memory хранилище очистится)

### Ошибка: "Connection refused" при вызове API

Убедитесь, что приложение запущено:
```bash
# Проверить процесс
ps aux | grep MaskAdmin

# Проверить порт
sudo netstat -tlnp | grep 5000

# Проверить логи
sudo journalctl -u maskadmin -n 50 --no-pager
```

### Ошибка: База данных не найдена

Примените миграции:
```bash
cd /opt/mask-browser/MaskAdmin
dotnet ef database update
```

### Ошибка: "Sequence contains no elements" при создании админа

Это означает, что последовательность ID для таблицы Users не синхронизирована.

Исправление:
```sql
-- Проверьте текущее значение последовательности
SELECT last_value FROM "Users_Id_seq";

-- Установите правильное значение (максимальный ID + 1)
SELECT setval('"Users_Id_seq"', (SELECT COALESCE(MAX("Id"), 0) + 1 FROM "Users"));
```

## Генерация BCrypt хеша в .NET

Если нужно сгенерировать хеш вручную:

```csharp
using BCrypt.Net;

var password = "Admin123!";
var hash = BCrypt.HashPassword(password, 11);
Console.WriteLine(hash);
// Output: $2a$11$...
```

Или через dotnet-script:
```bash
dotnet script -c "using BCrypt.Net; Console.WriteLine(BCrypt.HashPassword(\"Admin123!\", 11));"
```

## Безопасность

⚠️ **ВАЖНО:**
1. Всегда меняйте пароль по умолчанию в production
2. Используйте сильные пароли (минимум 12 символов, заглавные/строчные буквы, цифры, спецсимволы)
3. Ограничьте доступ к API endpoints создания/сброса админа через firewall
4. Регулярно проверяйте audit logs на подозрительную активность

## Примеры использования

### Первоначальная настройка
```bash
# 1. Применить миграции
cd /opt/mask-browser/MaskAdmin
dotnet ef database update

# 2. Запустить приложение
dotnet run &

# 3. Дождаться запуска (несколько секунд)
sleep 5

# 4. Создать администратора
./scripts/create-admin.sh "MySecurePassword123!"

# 5. Войти в систему
# Откройте браузер: http://localhost:5000/Auth/Login
```

### Восстановление доступа
```bash
# Если забыли пароль
./scripts/reset-password.sh "NewPassword123!"

# Если аккаунт заблокирован
psql -U postgres -d mask_browser -c "UPDATE \"Users\" SET \"IsBanned\" = false, \"IsActive\" = true WHERE \"Username\" = 'admin';"
```

### Production развертывание
```bash
# На production сервере
cd /opt/mask-browser/MaskAdmin/scripts

# Создать админа с безопасным паролем
./create-admin.sh "$(openssl rand -base64 32 | tr -d '/+=' | head -c 24)!Aa1" "https://admin.yourdomain.com"

# Сохраните сгенерированный пароль в безопасное место!
```
