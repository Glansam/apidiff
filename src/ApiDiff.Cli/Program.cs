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

        var compareCommand = new Command("compare", "Compare two OpenAPI specifications for breaking changes.")
        {
            oldOption,
            newOption,
            outOption,
            failOnBreakingOption
        };

        compareCommand.SetHandler(async (oldInput, newInput, outFile, failOnBreaking) =>
        {
            try
            {
                // Licensing checks for Pro features
                if (outFile != null)
                {
                    LicenseValidator.EnsurePro("Markdown Reporting");
                }
                if (failOnBreaking)
                {
                    LicenseValidator.EnsurePro("CI Fail-On-Breaking");
                }
                if (oldInput.StartsWith("http") || newInput.StartsWith("http"))
                {
                    LicenseValidator.EnsurePro("URL Input Scanning");
                }

                var oldJson = await LoadContentAsync(oldInput);
                var newJson = await LoadContentAsync(newInput);

                var engine = new DiffEngine();
                var results = engine.Compare(oldJson, newJson).ToList();

                bool hasBreaking = false;
                var markdownReport = new StringBuilder();
                markdownReport.AppendLine("# API Breaking Change Report\n");

                var breakingCount = results.Count(r => r.Severity == DiffSeverity.Breaking);
                if (breakingCount > 0)
                {
                    Console.WriteLine($"Found {breakingCount} breaking change(s).");
                }

                foreach (var result in results)
                {
                    if (result.Severity == DiffSeverity.Breaking)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        hasBreaking = true;
                        markdownReport.AppendLine($"- 🛑 **BREAKING**: {result.Message.Replace("BREAKING: ", "")}");
                    }
                    else if (result.Severity == DiffSeverity.Warning)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        markdownReport.AppendLine($"- ⚠️ **WARNING**: {result.Message.Replace("WARNING: ", "")}");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        markdownReport.AppendLine($"- ℹ️ **INFO**: {result.Message}");
                    }

                    Console.WriteLine(result.Message);
                    Console.ResetColor();
                }

                if (!results.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("No breaking changes detected.");
                    Console.ResetColor();
                    markdownReport.AppendLine("✅ No breaking changes detected.");
                }
                else if (failOnBreaking && hasBreaking)
                {
                    Environment.ExitCode = 2;
                }

                if (outFile != null)
                {
                    await File.WriteAllTextAsync(outFile.FullName, markdownReport.ToString());
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\nReport generated at: {outFile.FullName}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 64; // Set 64 for file/processing errors
            }
        }, oldOption, newOption, outOption, failOnBreakingOption);

        var rootCommand = new RootCommand("API Breaking Change Detector");
        rootCommand.AddCommand(compareCommand);

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
