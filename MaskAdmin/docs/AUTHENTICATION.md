# MaskAdmin Authentication System

## Обзор

MaskAdmin использует **Cookie-based Authentication** с ASP.NET Core Identity для обеспечения безопасного доступа к административной панели.

## Архитектура

### Компоненты

1. **AuthController** - Обрабатывает вход/выход пользователей
2. **Cookie Authentication** - Хранит сессию пользователя
3. **Claims-based Authorization** - Управление ролями и разрешениями
4. **Rate Limiting Middleware** - Защита от brute-force атак

### Схема аутентификации

```
User → Login Form → AuthController
              ↓
    Verify Credentials (BCrypt)
              ↓
    Check User Status (Active/Banned/Frozen)
              ↓
    Create Claims Identity
              ↓
    Sign In (Cookie)
              ↓
    Redirect to Dashboard
```

## Настройка

### Program.cs

```csharp
// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "MaskAdmin.Auth";
    });

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User", "Admin"));
});
```

### Параметры Cookie

| Параметр | Значение | Описание |
|----------|----------|----------|
| Name | `MaskAdmin.Auth` | Имя cookie |
| Expiration | 8 часов | Время жизни сессии |
| HttpOnly | `true` | Защита от XSS |
| Secure | `SameAsRequest` | HTTPS в production |
| SameSite | `Lax` | Защита от CSRF |
| Sliding | `true` | Продление сессии при активности |

## Процесс входа

### 1. GET /Auth/Login

Отображает форму входа:

```html
<form method="post" asp-action="Login" asp-controller="Auth">
    <input type="text" name="username" />
    <input type="password" name="password" />
    <button type="submit">Sign In</button>
</form>
```

### 2. POST /Auth/Login

Проверяет учетные данные:

```csharp
// 1. Найти пользователя
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Username == username || u.Email == username);

// 2. Проверить пароль (BCrypt)
bool passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

// 3. Проверить статус
if (!user.IsActive) // Account disabled
if (user.IsBanned)  // Account banned
if (user.IsFrozen)  // Account frozen

// 4. Создать Claims
var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new Claim(ClaimTypes.Name, user.Username),
    new Claim(ClaimTypes.Email, user.Email),
    new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
    new Claim("UserId", user.Id.ToString())
};

// 5. Войти в систему
await HttpContext.SignInAsync(
    CookieAuthenticationDefaults.AuthenticationScheme,
    new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) }
);
```

### 3. Редирект

После успешного входа:
- По умолчанию → `/Dashboard`
- Если указан `returnUrl` → к запрошенной странице

## Процесс выхода

### POST /Auth/Logout

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Logout()
{
    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return RedirectToAction("Login");
}
```

Удаляет cookie и завершает сессию.

## Авторизация

### Защита контроллеров

```csharp
// Требуется аутентификация
[Authorize]
public class DashboardController : Controller
{
    // ...
}

// Только администраторы
[Authorize(Policy = "AdminOnly")]
public class UsersController : Controller
{
    // ...
}

// Разрешить анонимный доступ
[AllowAnonymous]
public class AuthController : Controller
{
    // ...
}
```

### Проверка в коде

```csharp
// Проверить, аутентифицирован ли пользователь
if (User.Identity?.IsAuthenticated == true)
{
    // ...
}

// Получить ID пользователя
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

// Получить имя пользователя
var username = User.Identity?.Name;

// Проверить роль
if (User.IsInRole("Admin"))
{
    // ...
}

// Получить claim
var isAdmin = User.FindFirstValue("IsAdmin");
```

### Проверка в Razor Views

```cshtml
@if (User.Identity?.IsAuthenticated == true)
{
    <p>Welcome, @User.Identity.Name!</p>
}

@if (User.IsInRole("Admin"))
{
    <a href="/Users">Manage Users</a>
}
```

## Безопасность

### 1. Хеширование паролей (BCrypt)

```csharp
// Создание пароля
var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 11);

// Проверка пароля
bool isValid = BCrypt.Net.BCrypt.Verify(password, passwordHash);
```

**Параметры:**
- Work Factor: 11 (2^11 = 2048 итераций)
- Salt: Автоматически генерируется и включается в хеш

### 2. Rate Limiting

Встроенная защита от brute-force атак:

| Endpoint | Лимит | Период | Блокировка |
|----------|-------|--------|------------|
| `/Auth/Login` | 5 попыток | 60 секунд | 15 минут |
| API endpoints | 100 запросов | 60 секунд | - |

### 3. CSRF Protection

Все формы защищены от CSRF:

```cshtml
<form method="post">
    @Html.AntiForgeryToken()
    <!-- form fields -->
</form>
```

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(...)
```

### 4. XSS Protection

- Cookie с флагом `HttpOnly`
- Content Security Policy (CSP)
- Экранирование вывода в Razor

### 5. Secure Headers

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "no-referrer-when-downgrade");
    await next();
});
```

## Статусы пользователей

### IsActive

- `true` - Пользователь активен
- `false` - Аккаунт деактивирован

**Эффект:** Пользователь не может войти

### IsBanned

- `true` - Пользователь забанен
- `false` - Нет бана

**Эффект:** Пользователь не может войти, показывается "Account is banned"

### IsFrozen

- `true` - Аккаунт заморожен
- `false` - Нет заморозки

**Эффект:** Пользователь не может войти, показывается "Account is temporarily frozen"

## Логирование

### События аутентификации

Все попытки входа логируются:

```csharp
_logger.LogInformation("Login attempt for username: {Username}", username);
_logger.LogWarning("User not found: {Username}", username);
_logger.LogWarning("Invalid password for user: {Username}", username);
_logger.LogInformation("User {Username} (ID: {UserId}) logged in successfully", user.Username, user.Id);
```

### Audit Logs

При успешном входе обновляется:
- `LastLoginAt` - время последнего входа
- `LastLoginIp` - IP адрес

## Troubleshooting

### Проблема: "Invalid username or password"

**Причины:**
1. Неправильные учетные данные
2. Пользователь не существует
3. Неверный хеш пароля в базе данных

**Решение:**
```bash
# Проверить пользователя
psql -U postgres -d mask_browser -c "SELECT \"Username\", \"IsActive\", \"IsBanned\" FROM \"Users\" WHERE \"Username\" = 'admin';"

# Сбросить пароль
curl -X POST http://localhost:5000/reset-admin-password \
  -H "Content-Type: application/json" \
  -d '{"newPassword": "Admin123!"}'
```

### Проблема: "Account is disabled"

**Причина:** `IsActive = false`

**Решение:**
```sql
UPDATE "Users" SET "IsActive" = true WHERE "Username" = 'admin';
```

### Проблема: "Account is banned"

**Причина:** `IsBanned = true`

**Решение:**
```sql
UPDATE "Users" SET "IsBanned" = false WHERE "Username" = 'admin';
```

### Проблема: Cookie не сохраняется

**Причины:**
1. Браузер блокирует cookies
2. Неправильные настройки SameSite
3. HTTPS required, но используется HTTP

**Решение:**
- Проверьте настройки браузера
- Используйте HTTPS в production
- Проверьте `Cookie.SecurePolicy` в Program.cs

### Проблема: Сессия сбрасывается после перезапуска

**Причина:** Cookie хранится в памяти (не персистентные)

**Решение:** Использовать Data Protection с Redis:
```csharp
builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
```

## Создание администратора

### Способ 1: API endpoint

```bash
curl -X POST http://localhost:5000/create-admin \
  -H "Content-Type: application/json" \
  -d '{"password": "Admin123!"}'
```

### Способ 2: SQL

```sql
-- Сгенерируйте BCrypt хеш (используйте online tool или C# код)
-- Пример для "Admin123!": $2a$11$...

INSERT INTO "Users" (
    "Username", "Email", "PasswordHash", "Balance",
    "IsActive", "IsAdmin", "IsBanned", "IsFrozen",
    "TwoFactorEnabled", "CreatedAt"
)
VALUES (
    'admin', 'admin@maskbrowser.com', '$2a$11$YourHashHere', 0,
    true, true, false, false,
    false, NOW()
);
```

### Способ 3: Скрипт

```bash
cd /opt/mask-browser/MaskAdmin/scripts
./create-admin.sh "Admin123!"
```

## Миграция с JWT на Cookie

Если вы обновляете с предыдущей версии:

1. **Удалить старые imports:**
   - `Microsoft.AspNetCore.Authentication.JwtBearer`
   - `Microsoft.IdentityModel.Tokens`

2. **Обновить Program.cs:**
   - Заменить JWT на Cookie Authentication

3. **Обновить AuthController:**
   - Использовать `HttpContext.SignInAsync` вместо генерации JWT
   - Удалить метод `GenerateJwtToken`

4. **Перезапустить приложение:**
   ```bash
   sudo systemctl restart maskadmin
   ```

5. **Очистить старые cookies:**
   - Пользователи должны войти заново

## Best Practices

1. **Всегда используйте HTTPS в production**
2. **Включите HSTS** для принудительного использования HTTPS
3. **Регулярно обновляйте пароли** администраторов
4. **Мониторьте audit logs** на подозрительную активность
5. **Используйте strong passwords** (минимум 12 символов)
6. **Включите 2FA** для критичных аккаунтов (будущая функция)
7. **Ограничьте доступ по IP** через firewall
8. **Регулярно проверяйте** неудачные попытки входа
9. **Настройте alerts** для множественных неудачных попыток
10. **Используйте session timeouts** для неактивных пользователей

## Дополнительные ресурсы

- [ASP.NET Core Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/)
- [Cookie Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/cookie)
- [BCrypt Specification](https://en.wikipedia.org/wiki/Bcrypt)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
