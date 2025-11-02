using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace MaskBrowser.Server.Services;

public class RsaKeyService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RsaKeyService> _logger;
    private RSA? _rsa;

    public RsaKeyService(IConfiguration configuration, ILogger<RsaKeyService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        LoadOrGenerateRsaKeys();
    }

    private void LoadOrGenerateRsaKeys()
    {
        var privateKeyPath = _configuration["Jwt:RsaPrivateKeyPath"] ?? "./keys/rsa_private_key.pem";
        var publicKeyPath = _configuration["Jwt:RsaPublicKeyPath"] ?? "./keys/rsa_public_key.pem";

        _rsa = RSA.Create();

        try
        {
            // Try to load existing keys
            if (File.Exists(privateKeyPath) && File.Exists(publicKeyPath))
            {
                var existingPrivateKeyPem = File.ReadAllText(privateKeyPath);
                var existingPublicKeyPem = File.ReadAllText(publicKeyPath);
                
                _rsa.ImportFromPem(existingPrivateKeyPem);
                _logger.LogInformation("RSA keys loaded from files");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load RSA keys, generating new ones");
        }

        // Generate new keys
        _rsa.KeySize = 2048;
        
        var newPrivateKeyPem = _rsa.ExportRSAPrivateKeyPem();
        var newPublicKeyPem = _rsa.ExportRSAPublicKeyPem();

        // Ensure directory exists
        var directory = Path.GetDirectoryName(privateKeyPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(privateKeyPath, newPrivateKeyPem);
        File.WriteAllText(publicKeyPath, newPublicKeyPem);

        _logger.LogInformation("New RSA keys generated and saved");
    }

    public RsaSecurityKey GetPrivateKey()
    {
        if (_rsa == null)
        {
            throw new InvalidOperationException("RSA key not initialized");
        }
        return new RsaSecurityKey(_rsa);
    }

    public RsaSecurityKey GetPublicKey()
    {
        if (_rsa == null)
        {
            throw new InvalidOperationException("RSA key not initialized");
        }
        
        var publicRsa = RSA.Create();
        publicRsa.ImportRSAPublicKey(_rsa.ExportRSAPublicKey(), out _);
        return new RsaSecurityKey(publicRsa);
    }

    public string GetPublicKeyAsPem()
    {
        if (_rsa == null)
        {
            throw new InvalidOperationException("RSA key not initialized");
        }
        return _rsa.ExportRSAPublicKeyPem();
    }
}