# Двухфакторная аутентификация (2FA/TOTP)

## Обзор

Система поддерживает TOTP (Time-based One-Time Password) двухфакторную аутентификацию, совместимую с популярными приложениями-аутентификаторами.

## Поддерживаемые приложения

- Google Authenticator
- Microsoft Authenticator
- Authy
- 1Password
- Любые другие приложения, поддерживающие TOTP

## Включение 2FA

### 1. Запрос на включение

```http
POST /api/auth/two-factor/enable
Authorization: Bearer <token>
```

### 2. Ответ сервера

```json
{
  "secret": "JBSWY3DPEHPK3PXP",
  "qrCode": "base64_encoded_qr_code_image",
  "recoveryCodes": [
    "12345678",
    "87654321",
    "..."
  ]
}
```

### 3. Действия пользователя

1. Откройте приложение-аутентификатор на телефоне
2. Отсканируйте QR-код или введите secret вручную
3. Сохраните recovery codes в безопасном месте
4. Готово! 2FA теперь включен

## Вход с 2FA

### 1. Первый запрос (email + password)

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "password123"
}
```

### 2. Ответ сервера (требуется 2FA)

```http
HTTP/1.1 426 Upgrade Required

{
  "message": "Two-factor authentication required",
  "requires2FA": true
}
```

### 3. Второй запрос (с кодом 2FA)

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "password123",
  "twoFactorCode": "123456"
}
```

### 4. Успешный вход

```json
{
  "token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": 1,
    "username": "user123",
    "email": "user@example.com",
    "isAdmin": false,
    "twoFactorEnabled": true
  }
}
```

## Recovery Codes

Recovery codes используются для входа, если:
- Утерян доступ к приложению-аутентификатору
- Телефон потерян или поврежден
- Не синхронизируется время на устройстве

### Использование Recovery Code

Введите recovery code вместо TOTP кода в поле `twoFactorCode`. После использования код удаляется из списка.

⚠️ **ВАЖНО**: Каждый recovery code можно использовать только один раз!

## Отключение 2FA

```http
POST /api/auth/two-factor/disable
Authorization: Bearer <token>
Content-Type: application/json

{
  "password": "user_password"
}
```

Требуется подтверждение паролем для безопасности.

## Безопасность

1. **Recovery Codes**: Храните их в безопасном месте (менеджер паролей, зашифрованное хранилище)
2. **Backup**: Рекомендуется использовать приложения с облачной синхронизацией (Authy, 1Password)
3. **Утеря доступа**: Если потеряны все recovery codes и доступ к приложению, обратитесь в поддержку

## Troubleshooting

### Код не принимается

1. Проверьте, что время на устройстве синхронизировано
2. Убедитесь, что используете правильный код (коды обновляются каждые 30 секунд)
3. Попробуйте код с предыдущего или следующего интервала

### Утерян доступ

1. Используйте recovery code
2. Если recovery codes утеряны, обратитесь в поддержку
3. В критическом случае администратор может временно отключить 2FA

