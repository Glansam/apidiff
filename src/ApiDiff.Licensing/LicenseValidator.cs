using System;

namespace ApiDiff.Licensing;

public static class LicenseValidator
{
    // For this MVP, we simulate an offline check. 
    // Example: Key must start with PRO- and have length of 20
    public static bool IsPro()
    {
        var license = Environment.GetEnvironmentVariable("APIDIFF_LICENSE");
        
        if (string.IsNullOrWhiteSpace(license))
            return false;

        return license.StartsWith("PRO-") && license.Length == 20;
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
