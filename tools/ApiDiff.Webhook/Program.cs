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
    app.Logger.LogInformation("⬇️ Received POST request to /gumroad-webhook");
    
    // Dump all headers for debugging
    var headersDump = string.Join(", ", request.Headers.Select(h => $"{h.Key}: {h.Value}"));
    app.Logger.LogInformation("🔍 Incoming Headers: {Headers}", headersDump);

    // Dump raw body for debugging
    request.Body.Position = 0;
    using var reader = new StreamReader(request.Body, leaveOpen: true);
    var bodyString = await reader.ReadToEndAsync();
    request.Body.Position = 0;
    app.Logger.LogInformation("📦 Incoming Body: {Body}", bodyString);

    var gumroadSecret = builder.Configuration["GUMROAD_SECRET"];
    if (string.IsNullOrEmpty(gumroadSecret))
    {
        app.Logger.LogWarning("❌ GUMROAD_SECRET environment variable is missing.");
        return Results.StatusCode(500);
    }

    // 1. Enable buffering so we can read the body twice (once for HMAC, once for Form parsing)
    request.EnableBuffering();

    // 2. Read request body for signature validation
    using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
    var body = await reader.ReadToEndAsync();
    
    // Reset stream position for the next reader (FormReader)
    request.Body.Position = 0;

    // 3. Validate Signature
    if (!request.Headers.TryGetValue("X-Gumroad-Signature", out var signatureHeader))
    {
        // Gumroad's "Send test ping to URL" button often omits the signature header.
        app.Logger.LogWarning("❌ Missing X-Gumroad-Signature header. If this is a 'Test Ping' from the Gumroad dashboard, this is expected behavior.");
        return Results.BadRequest(new { error = "Missing signature header. Genuine purchases will have this header." });
    }

    var expectedSignature = CreateSignature(body, gumroadSecret);
    if (signatureHeader.ToString() != expectedSignature)
    {
        app.Logger.LogWarning("❌ Signature mismatch. Expected: {Expected}, Got: {Actual}", expectedSignature, signatureHeader.ToString());
        return Results.Unauthorized();
    }
    
    app.Logger.LogInformation("✅ Signature verified successfully.");

    // 4. Parse Form Body
    if (!request.HasFormContentType)
    {
        app.Logger.LogWarning("❌ Unsupported content type: {ContentType}", request.ContentType);
        return Results.BadRequest(new { error = "Unsupported content type" });
    }

    var form = await request.ReadFormAsync();
    var email = form["email"].ToString();
    
    if (string.IsNullOrEmpty(email))
    {
        app.Logger.LogWarning("❌ Email field not found in form data.");
        return Results.BadRequest(new { error = "Email not found" });
    }

    app.Logger.LogInformation("✅ Preparing to generate license for: {Email}", email);

    // 5. Generate License
    var privateKeyBase64 = builder.Configuration["APIDIFF_PRIVATE_KEY"];
    if (string.IsNullOrEmpty(privateKeyBase64))
    {
        app.Logger.LogWarning("❌ APIDIFF_PRIVATE_KEY environment variable is missing.");
        return Results.StatusCode(500);
    }
    
    try
    {
        var licenseKey = GenerateLicenseKey(email, privateKeyBase64);
        
        // 6. Send Email via Service (TODO: Implement actual sending, e.g. SendGrid)
        app.Logger.LogInformation("[Mock Email Send] To: {Email}", email);
        app.Logger.LogInformation("[Mock Email Send] Body: Your ApiDiff Pro License Key: {LicenseKey}", licenseKey);

        return Results.Ok(new { message = "License generated and email dispatched." });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "❌ Error generating license");
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
