namespace ApiDiff.Core.Models;

public enum DiffSeverity
{
    Info,
    Warning,
    Breaking
}

public record DiffEvent(string Message, DiffSeverity Severity);
