using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace ApiDiff.Cli.Tests;

public class ExitCodeTests : IDisposable
{
    private readonly string _cliToolPath;
    private readonly List<string> _tempFiles = new();

    public ExitCodeTests()
    {
        // Find the absolute path to ApiDiff.Cli.dll based on relative project paths
        var currentDir = Directory.GetCurrentDirectory(); 
        var projectDir = Directory.GetParent(currentDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
        
        if (projectDir == null) throw new InvalidOperationException("Could not find project root.");
        
        // We compile CLI logic into the same Debug/Release folder as the tests are running in ideal CI
        // But for local tests, we'll shell out to 'dotnet run' on the CLI project or point to the built dll
        
        var cliDll = Path.Combine(projectDir, "src", "ApiDiff.Cli", "bin", "Debug", "net8.0", "ApiDiff.Cli.dll");
        
        if (!File.Exists(cliDll))
        {
            // Fallback to Release if needed
            cliDll = Path.Combine(projectDir, "src", "ApiDiff.Cli", "bin", "Release", "net8.0", "ApiDiff.Cli.dll");
        }

        if (!File.Exists(cliDll))
        {
            throw new FileNotFoundException($"Could not find CLI dll at {cliDll}. Please build src/ApiDiff.Cli first.");
        }

        _cliToolPath = cliDll;
    }

    private string CreateTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private (int ExitCode, string StdOut, string StdErr) RunCli(params string[] args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{_cliToolPath}\" {string.Join(" ", args)}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.EnvironmentVariables["APIDIFF_TEST_ENV"] = "1";

        process.Start();
        
        // Timeout just in case
        if (!process.WaitForExit(10000))
        {
            process.Kill();
            throw new TimeoutException("CLI tool did not exit within 10 seconds.");
        }

        return (process.ExitCode, process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd());
    }

    [Fact]
    public void Compare_WithBreakingAndFailOnBreaking_ReturnsExitCode2()
    {
        var oldSchema = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""get"": {} } } }";
        var newSchema = @"{ ""openapi"": ""3.0.0"", ""paths"": {} }"; // Removed endpoint = breaking

        var oldFile = CreateTempFile(oldSchema);
        var newFile = CreateTempFile(newSchema);

        var result = RunCli("compare", "--old", $"\"{oldFile}\"", "--new", $"\"{newFile}\"", "--fail-on-breaking");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("BREAKING", result.StdOut);
    }

    [Fact]
    public void Compare_WithBreakingAndNoFailOnBreaking_ReturnsExitCode0()
    {
        var oldSchema = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""get"": {} } } }";
        var newSchema = @"{ ""openapi"": ""3.0.0"", ""paths"": {} }"; // Removed endpoint = breaking

        var oldFile = CreateTempFile(oldSchema);
        var newFile = CreateTempFile(newSchema);

        var result = RunCli("compare", "--old", $"\"{oldFile}\"", "--new", $"\"{newFile}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("BREAKING", result.StdOut);
    }

    [Fact]
    public void Compare_WithoutBreakingAndFailOnBreaking_ReturnsExitCode0()
    {
        var schema = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""get"": {} } } }"; // No diff

        var oldFile = CreateTempFile(schema);
        var newFile = CreateTempFile(schema);

        var result = RunCli("compare", "--old", $"\"{oldFile}\"", "--new", $"\"{newFile}\"", "--fail-on-breaking");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No breaking changes detected", result.StdOut);
    }

    [Fact]
    public void Compare_WithInvalidFile_ReturnsExitCode64AndStderr()
    {
        var result = RunCli("compare", "--old", "\"nonexistent.json\"", "--new", "\"nonexistent.json\"");

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("Error:", result.StdErr); // Should output to stderr now
    }

    [Fact]
    public void Compare_WithJsonFormat_ReturnsValidJson()
    {
        var oldSchema = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""get"": {} } } }";
        var newSchema = @"{ ""openapi"": ""3.0.0"", ""paths"": {} }"; // Removed endpoint = breaking

        var oldFile = CreateTempFile(oldSchema);
        var newFile = CreateTempFile(newSchema);

        var result = RunCli("compare", "--old", $"\"{oldFile}\"", "--new", $"\"{newFile}\"", "--format", "json");

        Assert.Equal(0, result.ExitCode);
        
        // Parse the JSON to ensure it's valid
        var doc = JsonDocument.Parse(result.StdOut);
        var root = doc.RootElement;
        
        Assert.True(root.TryGetProperty("tool", out _));
        Assert.True(root.TryGetProperty("summary", out _));
        Assert.True(root.TryGetProperty("findings", out var findings));
        Assert.Equal(1, findings.GetArrayLength());
        
        var firstFinding = findings[0];
        Assert.Equal("Breaking", firstFinding.GetProperty("severity").GetString());
        Assert.Equal("ENDPOINT_REMOVED", firstFinding.GetProperty("ruleId").GetString());
    }
}
