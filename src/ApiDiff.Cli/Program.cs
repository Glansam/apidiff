using System.CommandLine;
using ApiDiff.Core;
using ApiDiff.Core.Models;
using ApiDiff.Licensing;
using System.Text;

namespace ApiDiff.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var oldOption = new Option<string>(
            name: "--old",
            description: "The old OpenAPI specification file or URL.")
        {
            IsRequired = true
        };

        var newOption = new Option<string>(
            name: "--new",
            description: "The new OpenAPI specification file or URL.")
        {
            IsRequired = true
        };

        var outOption = new Option<FileInfo?>(
            name: "--out",
            description: "Output file for the Markdown report.");

        var failOnBreakingOption = new Option<bool>(
            name: "--fail-on-breaking",
            description: "Exit with code 1 if any breaking changes are detected.");

        var formatOption = new Option<string>(
            name: "--format",
            getDefaultValue: () => "text",
            description: "Output format: text, json, or markdown.");

        var compareCommand = new Command("compare", "Compare two OpenAPI specifications for breaking changes.")
        {
            oldOption,
            newOption,
            outOption,
            failOnBreakingOption,
            formatOption
        };

        compareCommand.SetHandler(async (oldInput, newInput, outFile, failOnBreaking, format) =>
        {
            try
            {
                // Licensing checks for Pro features
                var isTestEnv = Environment.GetEnvironmentVariable("APIDIFF_TEST_ENV") == "1";
                if (!isTestEnv)
                {
                    if (outFile != null || format.Equals("markdown", StringComparison.OrdinalIgnoreCase) || format.Equals("json", StringComparison.OrdinalIgnoreCase))
                    {
                        LicenseValidator.EnsurePro("Advanced Reporting");
                    }
                    if (failOnBreaking)
                    {
                        LicenseValidator.EnsurePro("CI Fail-On-Breaking");
                    }
                    if (oldInput.StartsWith("http") || newInput.StartsWith("http"))
                    {
                        LicenseValidator.EnsurePro("URL Input Scanning");
                    }
                }

                var oldJson = await LoadContentAsync(oldInput);
                var newJson = await LoadContentAsync(newInput);

                var engine = new DiffEngine();
                var results = engine.Compare(oldJson, newJson).ToList();

                var breakingCount = results.Count(r => r.Severity == DiffSeverity.Breaking);
                var warningCount = results.Count(r => r.Severity == DiffSeverity.Warning);
                var infoCount = results.Count(r => r.Severity == DiffSeverity.Info);
                bool hasBreaking = breakingCount > 0;

                string outputContent = "";

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    var report = new
                    {
                        tool = new { name = "ApiDiff", version = "0.1.1" },
                        summary = new { breaking = breakingCount, warning = warningCount, info = infoCount },
                        findings = results
                    };
                    outputContent = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true, 
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    });
                }
                else if (format.Equals("markdown", StringComparison.OrdinalIgnoreCase))
                {
                    var md = new StringBuilder();
                    md.AppendLine("## ApiDiff Report\n");
                    md.AppendLine($"**Summary:** {breakingCount} breaking, {warningCount} warnings, {infoCount} info\n");
                    
                    if (hasBreaking)
                    {
                        md.AppendLine($"### ❌ Breaking changes ({breakingCount})");
                        foreach (var r in results.Where(x => x.Severity == DiffSeverity.Breaking))
                        {
                            var op = r.Operation != null ? $"**{r.Operation.Method} {r.Operation.Path}** — " : "";
                            md.AppendLine($"- {op}{r.Message.Replace("BREAKING: ", "")}");
                        }
                    }
                    outputContent = md.ToString();
                }
                else // text
                {
                    var textOutput = new StringBuilder();
                    if (breakingCount > 0)
                    {
                        textOutput.AppendLine($"Found {breakingCount} breaking change(s).");
                    }
                    foreach (var result in results)
                    {
                        textOutput.AppendLine(result.Message);
                    }
                    if (!results.Any())
                    {
                        textOutput.AppendLine("✅ No breaking changes detected.");
                    }
                    outputContent = textOutput.ToString().TrimEnd();

                    // For console text format, we still want colorized output if writing to stdout
                    if (outFile == null)
                    {
                        if (breakingCount > 0) Console.WriteLine($"Found {breakingCount} breaking change(s).");
                        foreach (var result in results)
                        {
                            if (result.Severity == DiffSeverity.Breaking) Console.ForegroundColor = ConsoleColor.Red;
                            else if (result.Severity == DiffSeverity.Warning) Console.ForegroundColor = ConsoleColor.Yellow;
                            else Console.ForegroundColor = ConsoleColor.Gray;

                            Console.WriteLine(result.Message);
                            Console.ResetColor();
                        }
                        if (!results.Any())
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("No breaking changes detected.");
                            Console.ResetColor();
                        }
                    }
                }

                if (outFile != null)
                {
                    await File.WriteAllTextAsync(outFile.FullName, outputContent);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\nReport generated at: {outFile.FullName}");
                    Console.ResetColor();
                }
                else if (!format.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(outputContent);
                }

                if (failOnBreaking && hasBreaking)
                {
                    Environment.ExitCode = 2;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 64; // Set 64 for file/processing errors
            }
        }, oldOption, newOption, outOption, failOnBreakingOption, formatOption);

        var emailArg = new Argument<string>("email", "The user's email address");
        var licenseGenerateCommand = new Command("generate", "Generate a new ApiDiff Pro License (Requires APIDIFF_PRIVATE_KEY Env Var)")
        {
            emailArg
        };

        licenseGenerateCommand.SetHandler((email) =>
        {
            var privateKeyBase64 = Environment.GetEnvironmentVariable("APIDIFF_PRIVATE_KEY");
            if (string.IsNullOrWhiteSpace(privateKeyBase64))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("Error: APIDIFF_PRIVATE_KEY environment variable is missing.");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            try
            {
                var payload = new LicensePayload
                {
                    Email = email,
                    ExpirationDate = DateTime.UtcNow.AddYears(1),
                    Tier = "Pro"
                };

                var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                
                using var rsa = System.Security.Cryptography.RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);

                var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
                var signatureBytes = rsa.SignData(payloadBytes, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);

                var signatureBase64 = Convert.ToBase64String(signatureBytes);
                var token = $"{payloadJson}.{signatureBase64}";

                // Base64Url encode
                var finalKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(token))
                                      .Replace('+', '-').Replace('/', '_').TrimEnd('=');

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"License generated for {email}");
                Console.ResetColor();
                Console.WriteLine($"\nexport APIDIFF_LICENSE={finalKey}\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error generating license: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 1;
            }
        }, emailArg);

        var licenseCommand = new Command("license", "Manage API Diff licenses")
        {
            licenseGenerateCommand
        };

        var rootCommand = new RootCommand("API Breaking Change Detector");
        rootCommand.AddCommand(compareCommand);
        rootCommand.AddCommand(licenseCommand);

        var result = await rootCommand.InvokeAsync(args);
        return Environment.ExitCode != 0 ? Environment.ExitCode : result;
    }

    private static async Task<string> LoadContentAsync(string pathOrUrl)
    {
        if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var client = new HttpClient();
            return await client.GetStringAsync(pathOrUrl);
        }

        return await File.ReadAllTextAsync(pathOrUrl);
    }
}
