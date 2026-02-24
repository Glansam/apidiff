using System.Text.Json.Serialization;

namespace ApiDiff.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiffSeverity
{
    Info,
    Warning,
    Breaking
}

public class DiffEvent
{
    public DiffSeverity Severity { get; init; }
    public string RuleId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DiffOperation? Operation { get; init; }
    public DiffLocation? Location { get; init; }
    public Dictionary<string, object>? Details { get; init; }

    public DiffEvent(DiffSeverity severity, string ruleId, string message)
    {
        Severity = severity;
        RuleId = ruleId;
        Message = message;
    }
}

public class DiffOperation
{
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}

public class DiffLocation
{
    public string? Area { get; init; }
    public string? ContentType { get; init; }
    public string? JsonPointer { get; init; }
}
