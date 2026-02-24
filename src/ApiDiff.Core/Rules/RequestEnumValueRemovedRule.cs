using ApiDiff.Core.Interfaces;
using ApiDiff.Core.Models;
using Microsoft.OpenApi.Models;

namespace ApiDiff.Core.Rules;

/// <summary>
/// Rule #5: Request Enum Value Removed
/// Checks if a previously accepted enum value is removed from a property in the application/json request body schema.
/// </summary>
public sealed class RequestEnumValueRemovedRule : IApiDiffRule
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

                        if (oldProp.Enum != null && oldProp.Enum.Any())
                        {
                            var oldEnums = oldProp.Enum.Select(e => ((Microsoft.OpenApi.Any.OpenApiString)e).Value).ToList();
                            var newEnums = newProp.Enum?.Select(e => ((Microsoft.OpenApi.Any.OpenApiString)e).Value).ToList() ?? new List<string>();

                            foreach (var oldEnum in oldEnums)
                            {
                                if (!newEnums.Contains(oldEnum))
                                {
                                    yield return new DiffEvent(DiffSeverity.Breaking, "REQ_ENUM_VALUE_REMOVED", $"BREAKING: {key} request field '{prop.Key}' removed enum value '{oldEnum}'")
                                    {
                                        Operation = new DiffOperation { Method = method.ToString().ToUpperInvariant(), Path = path },
                                        Location = new DiffLocation { Area = "requestBody", ContentType = "application/json" },
                                        Details = new Dictionary<string, object> 
                                        { 
                                            { "field", prop.Key },
                                            { "removedValue", oldEnum }
                                        }
                                    };
                                }
                            }
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
