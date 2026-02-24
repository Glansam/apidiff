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
                        yield return new DiffEvent(DiffSeverity.Breaking, "RES_FIELD_REMOVED", $"BREAKING: {key} response removed field '{prop.Key}'")
                        {
                            Operation = new DiffOperation { Method = method.ToString().ToUpperInvariant(), Path = path },
                            Location = new DiffLocation { Area = "responses", ContentType = "application/json" },
                            Details = new Dictionary<string, object> { { "field", prop.Key } }
                        };
                    }
                    else if (prop.Value.Type != newProp.Type)
                    {
                        yield return new DiffEvent(DiffSeverity.Breaking, "RES_FIELD_TYPE_CHANGED", $"BREAKING: {key} response field '{prop.Key}' changed type from {prop.Value.Type ?? "null"} to {newProp.Type ?? "null"}")
                        {
                            Operation = new DiffOperation { Method = method.ToString().ToUpperInvariant(), Path = path },
                            Location = new DiffLocation { Area = "responses", ContentType = "application/json" },
                            Details = new Dictionary<string, object> 
                            { 
                                { "field", prop.Key },
                                { "oldType", prop.Value.Type ?? "null" },
                                { "newType", newProp.Type ?? "null" }
                            }
                        };
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
