using ApiDiff.Core.Interfaces;
using ApiDiff.Core.Models;
using Microsoft.OpenApi.Models;

namespace ApiDiff.Core.Rules;

/// <summary>
/// Rule #2 (Extension): Required Request Body Added
/// Checks if a request body was previously absent (or optional) but is now marked as required in the new specification.
/// </summary>
public sealed class RequestBodyAddedRule : IApiDiffRule
{
    public IEnumerable<DiffEvent> Evaluate(DiffContext context)
    {
        foreach (var (path, method, oldOp, newOp) in context.CommonOperations)
        {
            var key = $"{method.ToString().ToUpperInvariant()} {path}";
            var oldSchema = GetJsonSchema(oldOp.RequestBody);
            var newSchema = GetJsonSchema(newOp.RequestBody);

            if (oldSchema == null && newSchema != null)
            {
                if (newOp.RequestBody.Required)
                {
                    yield return new DiffEvent(DiffSeverity.Breaking, "REQ_BODY_ADDED", $"BREAKING: {key} added a required request body")
                    {
                        Operation = new DiffOperation { Method = method.ToString().ToUpperInvariant(), Path = path },
                        Location = new DiffLocation { Area = "requestBody", ContentType = "application/json" }
                    };
                }
            }
            else if ((oldOp.RequestBody?.Required ?? false) == false && (newOp.RequestBody?.Required ?? false) == true)
            {
                yield return new DiffEvent(DiffSeverity.Breaking, "REQ_BODY_BECAME_REQUIRED", $"BREAKING: request body became required for {key}")
                {
                    Operation = new DiffOperation { Method = method.ToString().ToUpperInvariant(), Path = path },
                    Location = new DiffLocation { Area = "requestBody", ContentType = "application/json" }
                };
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
