using System.Text.RegularExpressions;

namespace MaskAdmin.Services;

public static class PasswordValidator
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static (bool IsValid, List<string> Errors) Validate(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required");
            return (false, errors);
        }

        if (password.Length < MinLength)
        {
            errors.Add($"Password must be at least {MinLength} characters long");
        }

        if (password.Length > MaxLength)
        {
            errors.Add($"Password must not exceed {MaxLength} characters");
        }

        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            errors.Add("Password must contain at least one lowercase letter");
        }

        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            errors.Add("Password must contain at least one uppercase letter");
        }

        if (!Regex.IsMatch(password, @"\d"))
        {
            errors.Add("Password must contain at least one digit");
        }

        if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>/?]"))
        {
            errors.Add("Password must contain at least one special character (!@#$%^&*()_+-=[]{}; etc.)");
        }

        // Check for common weak passwords
        var weakPasswords = new[] {
            "password", "12345678", "qwerty123", "admin123", "letmein",
            "welcome1", "Password1", "Passw0rd", "Admin123!"
        };

        if (weakPasswords.Any(weak => password.Equals(weak, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("Password is too common. Please choose a more secure password");
        }

        return (errors.Count == 0, errors);
    }

    public static string GetPasswordRequirements()
    {
        return $@"Password requirements:
- At least {MinLength} characters long
- Contains uppercase and lowercase letters
- Contains at least one digit
- Contains at least one special character (!@#$%^&*()_+-=[]{{}}; etc.)
- Not a common password";
    }
}
