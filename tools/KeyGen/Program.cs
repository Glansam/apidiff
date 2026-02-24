using System;
using System.Security.Cryptography;

namespace KeyGen;

class Program
{
    static void Main()
    {
        using var rsa = RSA.Create();
        Console.WriteLine("--- PUBLIC KEY (Embed in LicenseValidator) ---");
        Console.WriteLine(Convert.ToBase64String(rsa.ExportRSAPublicKey()));
        
        Console.WriteLine("\n--- PRIVATE KEY (Keep Secret) ---");
        Console.WriteLine(Convert.ToBase64String(rsa.ExportRSAPrivateKey()));
    }
}
