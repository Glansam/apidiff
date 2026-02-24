using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using ApiDiff.Licensing;

namespace ApiDiff.Licensing.Tests;

public class LicenseValidatorTests : IDisposable
{
    private readonly string _privateKeyBase64;
    private readonly string _originalPublicKey;

    public LicenseValidatorTests()
    {
        // Generate a temporary RSA KeyPair for testing so we can sign payloads
        using var rsa = RSA.Create();
        _privateKeyBase64 = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
        
        // Save original key and inject our test public key
        _originalPublicKey = LicenseValidator.PublicKeyBase64;
        LicenseValidator.PublicKeyBase64 = Convert.ToBase64String(rsa.ExportRSAPublicKey());

        // Reset cache explicitly
        typeof(LicenseValidator).GetField("_isProCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("APIDIFF_LICENSE", null);
        LicenseValidator.PublicKeyBase64 = _originalPublicKey;
        typeof(LicenseValidator).GetField("_isProCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, null);
    }

    private string GenerateToken(LicensePayload payload, string privateKeyBase64)
    {
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);

        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var signatureBytes = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var signatureBase64 = Convert.ToBase64String(signatureBytes);
        var token = $"{payloadJson}.{signatureBase64}";

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(token)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    [Fact]
    public void IsPro_WhenLicenseIsMissing_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("APIDIFF_LICENSE", null);
        Assert.False(LicenseValidator.IsPro());
    }

    [Fact]
    public void IsPro_WhenLicenseIsValidAndNotExpired_ReturnsTrue()
    {
        var payload = new LicensePayload { Email = "test@example.com", ExpirationDate = DateTime.UtcNow.AddYears(1), Tier = "Pro" };
        var token = GenerateToken(payload, _privateKeyBase64);
        
        Environment.SetEnvironmentVariable("APIDIFF_LICENSE", token);
        Assert.True(LicenseValidator.IsPro());
    }

    [Fact]
    public void IsPro_WhenLicenseIsExpired_ReturnsFalse()
    {
        var payload = new LicensePayload { Email = "test@example.com", ExpirationDate = DateTime.UtcNow.AddMinutes(-1), Tier = "Pro" };
        var token = GenerateToken(payload, _privateKeyBase64);
        
        Environment.SetEnvironmentVariable("APIDIFF_LICENSE", token);
        Assert.False(LicenseValidator.IsPro());
    }

    [Fact]
    public void IsPro_WhenLicenseIsTampered_ReturnsFalse()
    {
        var payload = new LicensePayload { Email = "test@example.com", ExpirationDate = DateTime.UtcNow.AddYears(1), Tier = "Pro" };
        var token = GenerateToken(payload, _privateKeyBase64);
        
        // Tamper with the token (e.g. change a character in the Base64urL)
        var tamperedToken = token.Substring(0, token.Length - 5) + "aaaaa";
        
        Environment.SetEnvironmentVariable("APIDIFF_LICENSE", tamperedToken);
        Assert.False(LicenseValidator.IsPro());
    }
}
