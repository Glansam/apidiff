using ApiDiff.Core.Interfaces;
using ApiDiff.Core.Models;
using Microsoft.OpenApi.Models;

namespace ApiDiff.Core.Rules;

/// <summary>
/// Rule #4: Response Field Removed or Type Changed
/// Checks if a property in a 2xx response schema (application/json) is either removed entirely or has changed its data type.
/// </summary>
public sealed class ResponseFieldChangedRule : IApiDiffRule
{
    public IEnumerable<DiffEvent> Evaluate(DiffContext context)
    {
        foreach (var (path, method, oldOp, newOp) in context.CommonOperations)
        {
            var key = $"{method.ToString().ToUpperInvariant()} {path}";
            var oldSchema = GetSuccessResponseSchema(oldOp);
            var newSchema = GetSuccessResponseSchema(newOp);

            if (oldSchema?.Properties != null && newSchema != null)
            {
                var newProps = newSchema.Properties ?? new Dictionary<string, OpenApiSchema>();

                foreach (var prop in oldSchema.Properties)
                {
                    if (!newProps.TryGetValue(prop.Key, out var newProp))
                    {
                        yield return new DiffEvent($"BREAKING: {key} response removed field '{prop.Key}'", DiffSeverity.Breaking);
                    }
                    else if (prop.Value.Type != newProp.Type)
                    {
                        yield return new DiffEvent($"BREAKING: {key} response field '{prop.Key}' changed type from {prop.Value.Type ?? "null"} to {newProp.Type ?? "null"}", DiffSeverity.Breaking);
                    }
                }
            }
        }
    }

    private static OpenApiSchema? GetSuccessResponseSchema(OpenApiOperation op)
    {
        if (op.Responses == null) return null;

        var successResponse = op.Responses.FirstOrDefault(r => r.Key.StartsWith("2")).Value;
        if (successResponse?.Content != null && successResponse.Content.TryGetValue("application/json", out var mediaType))
        {
            return mediaType.Schema;
        }
        
        return null;
    }
}
