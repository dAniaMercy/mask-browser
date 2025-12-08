-- Check if admin user exists
SELECT
    "Id",
    "Username",
    "Email",
    "IsActive",
    "IsAdmin",
    "IsBanned",
    "CreatedAt"
FROM "Users"
WHERE "IsAdmin" = true;

-- If no admin exists, you can create one with this command:
-- Note: You need to generate a BCrypt hash for your password first
-- Example hash for "Admin123!": $2a$11$xYZ... (use online BCrypt generator or C# code)

-- Example INSERT (replace password hash):
/*
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
    '$2a$11$YourBCryptHashHere', -- Replace with actual hash
    0,
    true,
    true,
    false,
    false,
    false,
    NOW()
) ON CONFLICT ("Username") DO NOTHING;
*/
