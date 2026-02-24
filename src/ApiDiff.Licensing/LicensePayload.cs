using System;

namespace ApiDiff.Licensing;

public class LicensePayload
{
    public string Email { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public string Tier { get; set; } = "Pro";
}
