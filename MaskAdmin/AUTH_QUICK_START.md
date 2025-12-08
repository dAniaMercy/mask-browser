# Quick Start: Новая система аутентификации

## Что изменилось

✅ **Переход с JWT на Cookie Authentication**
- Более надежная система
- Автоматическое управление сессиями
- Лучшая интеграция с ASP.NET Core

✅ **Упрощенный AuthController**
- Убрана сложная логика с SQL
- Прямая работа с Entity Framework
- Улучшенное логирование

✅ **Проверки статусов**
- IsActive - аккаунт активен
- IsBanned - аккаунт забанен
- IsFrozen - аккаунт заморожен

## На сервере

### 1. Обновите код

```bash
cd /opt/mask-browser/MaskAdmin/scripts
sudo ./deploy.sh
```

Или вручную:
```bash
cd /opt/mask-browser/MaskAdmin
git pull origin main
dotnet build -c Release
dotnet ef database update
sudo systemctl restart maskadmin
```

### 2. Создайте администратора

Если администратора нет:

```bash
cd /opt/mask-browser/MaskAdmin/scripts
./create-admin.sh "Admin123!"
```

Или через API:
```bash
curl -X POST http://localhost:5000/create-admin \
  -H "Content-Type: application/json" \
  -d '{"password": "Admin123!"}'
```

### 3. Войдите в систему

Откройте: `http://your-server/Auth/Login`

**Учетные данные:**
- Username: `admin`
- Password: `Admin123!` (или ваш пароль)

## Если не работает

### 1. Проверьте пользователя в БД

```bash
psql -U postgres -d mask_browser -c "SELECT \"Id\", \"Username\", \"Email\", \"IsActive\", \"IsAdmin\", \"IsBanned\", \"IsFrozen\" FROM \"Users\" WHERE \"Username\" = 'admin';"
```

**Должно быть:**
- `IsActive` = `true`
- `IsAdmin` = `true`
- `IsBanned` = `false`
- `IsFrozen` = `false`

### 2. Сбросьте пароль

```bash
./scripts/reset-password.sh "Admin123!"
```

### 3. Разблокируйте аккаунт

```sql
UPDATE "Users"
SET "IsActive" = true, "IsBanned" = false, "IsFrozen" = false
WHERE "Username" = 'admin';
```

### 4. Проверьте логи

```bash
# Логи приложения
sudo journalctl -u maskadmin -n 100 --no-pager | grep -i "login\|auth"

# Логи файла
tail -f /opt/mask-browser/MaskAdmin/logs/maskadmin-*.log
```

Ищите строки:
- `Login attempt for username: admin`
- `User found: admin`
- `Password verification result`
- `logged in successfully`

## Тестирование

### Успешный вход

```bash
curl -X POST http://localhost:5000/Auth/Login \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=admin&password=Admin123!" \
  -c cookies.txt \
  -L
```

Должен вернуть HTML страницы Dashboard и сохранить cookie в `cookies.txt`.

### Проверка cookie

```bash
cat cookies.txt | grep MaskAdmin.Auth
```

Должна быть строка с cookie `MaskAdmin.Auth`.

### Доступ к защищенной странице

```bash
curl http://localhost:5000/Dashboard \
  -b cookies.txt
```

Должен вернуть HTML Dashboard (не редирект на Login).

## Основные изменения в коде

### До (JWT):

```csharp
// Генерация токена
var token = GenerateJwtToken(user);
Response.Cookies.Append("auth_token", token, cookieOptions);

// Middleware для чтения токена из cookie
options.Events = new JwtBearerEvents { ... };
```

### После (Cookie):

```csharp
// Создание Claims Identity
var claims = new List<Claim> { ... };
var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

// Вход в систему
await HttpContext.SignInAsync(
    CookieAuthenticationDefaults.AuthenticationScheme,
    new ClaimsPrincipal(claimsIdentity),
    authProperties);
```

## Безопасность

### Cookie параметры

```csharp
Cookie.HttpOnly = true;        // Защита от XSS
Cookie.SecurePolicy = SameAsRequest; // HTTPS в production
Cookie.SameSite = Lax;         // Защита от CSRF
```

### Rate Limiting

- **5 попыток** входа за 60 секунд
- **Блокировка на 15 минут** после превышения лимита

### Защита форм

Все POST формы защищены Anti-CSRF токеном:

```cshtml
<form method="post">
    @Html.AntiForgeryToken()
    ...
</form>
```

## Полная документация

См. [docs/AUTHENTICATION.md](docs/AUTHENTICATION.md) для подробной информации.

## Поддержка

Если возникли проблемы:

1. Проверьте логи: `sudo journalctl -u maskadmin -f`
2. Проверьте БД: статус пользователя, хеш пароля
3. Пересоздайте админа: `./scripts/create-admin.sh`
4. Откройте issue на GitHub

## Контрольный список

- [ ] Код обновлен из GitHub
- [ ] Приложение пересобрано
- [ ] Миграции применены
- [ ] Служба перезапущена
- [ ] Администратор создан
- [ ] Вход работает
- [ ] Dashboard доступен
- [ ] Cookie сохраняется
- [ ] Выход работает

✅ Если все пункты отмечены - система аутентификации работает!
