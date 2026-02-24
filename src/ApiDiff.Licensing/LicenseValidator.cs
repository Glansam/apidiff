using System;
using System.Security.Cryptography;

namespace ApiDiff.Licensing;

public static class LicenseValidator
{
    // Keep this public key embedded in the app
    public static string PublicKeyBase64 = "MIIBCgKCAQEAwEGtiJaWj3IAgkt8ZdkOJqOelIM0mOI+sNwHxxcPiQXv9WQ3A0r0TgmwtfIAls/M0tpl48VLuTe7NxkiKwPixBdLUmdwOs+JBSLHINYUwIwxgsdMQn7T214N2UVJuPeVx/KZ0xGG7MROImU9jPt7XbC0VaLYm315+B9TEyFaS8h+bdwLz6IOmhpYqD8XDQStzl1FQ2ycBS2/LYL+G3yY1VZh2JX283mRW+/etR5jfPZXHA3V9gp/SXytW13wLROpGYqxTyzuFbn/vQem/36bXbGhEJUu9pzmBLkQ0+18sHodZIBgDZGGmXvact3z1npp6bb0gpE1V1a1Ghk1hJK2qQIDAQAB";

    private static bool? _isProCache;

    public static bool IsPro()
    {
        if (_isProCache.HasValue) return _isProCache.Value; // Cache to avoid verifying multiple times per run

        var licenseKey = Environment.GetEnvironmentVariable("APIDIFF_LICENSE");
        
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            _isProCache = false;
            return false;
        }

        try
        {
            // Format: Base64Url(Payload.Signature)
            var paddedKey = licenseKey.Replace('-', '+').Replace('_', '/');
            switch (paddedKey.Length % 4)
            {
                case 2: paddedKey += "=="; break;
                case 3: paddedKey += "="; break;
            }

            var decodedBytes = Convert.FromBase64String(paddedKey);
            var decodedString = System.Text.Encoding.UTF8.GetString(decodedBytes);

            var lastDot = decodedString.LastIndexOf('.');
            if (lastDot == -1) 
            {
                _isProCache = false;
                return false;
            }

            var payloadJson = decodedString.Substring(0, lastDot);
            var signatureBase64 = decodedString.Substring(lastDot + 1);

            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(PublicKeyBase64), out _);

            var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payloadJson);
            var signatureBytes = Convert.FromBase64String(signatureBase64);

            var isValidSignature = rsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!isValidSignature)
            {
                _isProCache = false;
                return false;
            }

            var payload = System.Text.Json.JsonSerializer.Deserialize<LicensePayload>(payloadJson, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            
            if (payload == null || payload.ExpirationDate < DateTime.UtcNow)
            {
                _isProCache = false;
                return false;
            }

            _isProCache = payload.Tier == "Pro";
            return _isProCache.Value;
        }
        catch
        {
            _isProCache = false;
            return false;
        }
    }

    public static void EnsurePro(string featureName)
    {
        if (!IsPro())
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n[ApiDiff Pro Required]");
            Console.WriteLine($"The '{featureName}' feature requires an active ApiDiff Pro License.");
            Console.WriteLine($"Please set the APIDIFF_LICENSE environment variable to your key.");
            Console.WriteLine($"Get your key at: https://gumroad.com/");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }
}
