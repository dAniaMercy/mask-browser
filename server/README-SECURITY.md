# RSA256 JWT и 2FA - Настройка

## Генерация RSA ключей

При первом запуске сервера RSA ключи генерируются автоматически и сохраняются в:
- `./keys/rsa_private_key.pem` (приватный ключ)
- `./keys/rsa_public_key.pem` (публичный ключ)

## Настройка в appsettings.json

```json
{
  "Jwt": {
    "Issuer": "MaskBrowser",
    "Audience": "MaskBrowser",
    "ExpirationMinutes": 15,
    "RsaPrivateKeyPath": "./keys/rsa_private_key.pem",
    "RsaPublicKeyPath": "./keys/rsa_public_key.pem"
  }
}
```

## Миграция базы данных

После добавления полей 2FA в модель User необходимо выполнить миграцию:

```bash
dotnet ef migrations add AddTwoFactorAuthentication
dotnet ef database update
```

## Тестирование 2FA

### 1. Включение 2FA

```bash
curl -X POST http://localhost:5050/api/auth/two-factor/enable \
  -H "Authorization: Bearer <token>"
```

Ответ:
```json
{
  "secret": "JBSWY3DPEHPK3PXP",
  "qrCode": "base64_encoded_qr_code_image",
  "recoveryCodes": ["12345678", "87654321", ...]
}
```

### 2. Вход с 2FA

```bash
# Первый запрос (требуется 2FA)
curl -X POST http://localhost:5050/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "password123"
  }'
# Ответ: 426 (Two-factor authentication required)

# Второй запрос (с кодом 2FA)
curl -X POST http://localhost:5050/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "password123",
    "twoFactorCode": "123456"
  }'
```

## Безопасность

⚠️ **ВАЖНО**: 
- Приватный RSA ключ НЕ должен попадать в репозиторий
- Recovery codes нужно хранить в безопасном месте
- Регулярно создавайте бэкап ключей

