using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApiDiff.Licensing;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok("OK - apidiff license webhook is running"));

app.MapGet("/health", () => 
{
    app.Logger.LogInformation("✅ [TEST LOG] The /health endpoint was just called!");
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
});

app.MapPost("/gumroad-webhook", async (HttpRequest request) =>
{
    var gumroadSecret = builder.Configuration["GUMROAD_SECRET"];
    if (string.IsNullOrEmpty(gumroadSecret))
    {
        return Results.StatusCode(500);
    }

    // 1. Read request body
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    // 2. Validate Signature
    if (!request.Headers.TryGetValue("X-Gumroad-Signature", out var signatureHeader))
    {
        return Results.Unauthorized();
    }

    var expectedSignature = CreateSignature(body, gumroadSecret);
    if (signatureHeader.ToString() != expectedSignature)
    {
        return Results.Unauthorized();
    }

    // 3. Parse Body (FormUrlEncoded usually for Gumroad)
    // For simplicity, assuming JSON or we can use Form parsing if needed.
    // Gumroad usually sends application/x-www-form-urlencoded
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Unsupported content type" });
    }

    var form = await request.ReadFormAsync();
    var email = form["email"].ToString();
    
    if (string.IsNullOrEmpty(email))
    {
        return Results.BadRequest(new { error = "Email not found" });
    }

    // 4. Generate License
    var privateKeyBase64 = builder.Configuration["APIDIFF_PRIVATE_KEY"];
    if (string.IsNullOrEmpty(privateKeyBase64))
    {
        return Results.StatusCode(500);
    }
    
    try
    {
        var licenseKey = GenerateLicenseKey(email, privateKeyBase64);
        
        // 5. Send Email via Service (TODO: Implement actual sending, e.g. SendGrid)
        app.Logger.LogInformation("[Mock Email Send] To: {Email}", email);
        app.Logger.LogInformation("[Mock Email Send] Body: Your ApiDiff Pro License Key: {LicenseKey}", licenseKey);

        return Results.Ok(new { message = "License generated and email dispatched." });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error generating license");
        return Results.StatusCode(500);
    }
});

app.Run();

static string CreateSignature(string payload, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    return Convert.ToHexString(hash).ToLower();
}

static string GenerateLicenseKey(string email, string privateKeyBase64)
{
    var payload = new LicensePayload
    {
        Email = email,
        ExpirationDate = DateTime.UtcNow.AddYears(1),
        Tier = "Pro"
    };

    var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    
    using var rsa = RSA.Create();
    rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);

    var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
    var signatureBytes = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    var signatureBase64 = Convert.ToBase64String(signatureBytes);
    var token = $"{payloadJson}.{signatureBase64}";

    return Convert.ToBase64String(Encoding.UTF8.GetBytes(token))
                  .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
