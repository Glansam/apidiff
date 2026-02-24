using ApiDiff.Core.Interfaces;
using ApiDiff.Core.Models;
using Microsoft.OpenApi.Models;

namespace ApiDiff.Core.Rules;

/// <summary>
/// Rule #3: Request Field Type Changed
/// Checks if an existing property in the application/json request body schema has changed its data type.
/// </summary>
public sealed class RequestFieldTypeChangedRule : IApiDiffRule
{
    public IEnumerable<DiffEvent> Evaluate(DiffContext context)
    {
        foreach (var (path, method, oldOp, newOp) in context.CommonOperations)
        {
            var key = $"{method.ToString().ToUpperInvariant()} {path}";
            var oldSchema = GetJsonSchema(oldOp.RequestBody);
            var newSchema = GetJsonSchema(newOp.RequestBody);

            if (oldSchema?.Properties != null && newSchema?.Properties != null)
            {
                foreach (var prop in oldSchema.Properties)
                {
                    if (newSchema.Properties.TryGetValue(prop.Key, out var newProp))
                    {
                        var oldProp = prop.Value;
                        if (oldProp.Type != newProp.Type)
                        {
                            yield return new DiffEvent(DiffSeverity.Breaking, "REQ_FIELD_TYPE_CHANGED", $"BREAKING: {key} request field '{prop.Key}' changed type from {oldProp.Type ?? "null"} to {newProp.Type ?? "null"}")
                            {
                                Operation = new DiffOperation { Method = method.ToString().ToUpperInvariant(), Path = path },
                                Location = new DiffLocation { Area = "requestBody", ContentType = "application/json" },
                                Details = new Dictionary<string, object> 
                                { 
                                    { "field", prop.Key },
                                    { "oldType", oldProp.Type ?? "null" },
                                    { "newType", newProp.Type ?? "null" }
                                }
                            };
                        }
                    }
                }
            }
        }
    }

    private static OpenApiSchema? GetJsonSchema(OpenApiRequestBody? body)
    {
        if (body?.Content != null && body.Content.TryGetValue("application/json", out var mediaType))
        {
            return mediaType.Schema;
        }
        return null;
    }
}
