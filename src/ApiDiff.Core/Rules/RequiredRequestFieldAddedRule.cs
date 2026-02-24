using ApiDiff.Core.Interfaces;
using ApiDiff.Core.Models;
using Microsoft.OpenApi.Models;

namespace ApiDiff.Core.Rules;

/// <summary>
/// Rule #2: Request Required Field Added
/// Checks if a new required property is added to the application/json request body schema, which would break existing clients.
/// </summary>
public sealed class RequiredRequestFieldAddedRule : IApiDiffRule
{
    public IEnumerable<DiffEvent> Evaluate(DiffContext context)
    {
        foreach (var (path, method, oldOp, newOp) in context.CommonOperations)
        {
            // Only check application/json for MVP
            var oldSchema = TryGetJsonRequestSchema(oldOp);
            var newSchema = TryGetJsonRequestSchema(newOp);

            if (newSchema == null) continue; // nothing to validate
            if (newSchema.Type != "object") continue;

            var oldRequired = GetTopLevelRequired(oldSchema);
            var newRequired = GetTopLevelRequired(newSchema);

            // New required fields = breaking
            var added = newRequired.Except(oldRequired).ToArray();
            if (added.Length == 0) continue;

            foreach (var field in added)
            {
                yield return new DiffEvent(DiffSeverity.Breaking, "REQ_FIELD_ADDED", $"BREAKING: required field '{field}' added to request body for {method.ToString().ToUpperInvariant()} {path} (application/json)")
                {
                    Operation = new DiffOperation { Method = method.ToString().ToUpperInvariant(), Path = path },
                    Location = new DiffLocation { Area = "requestBody", ContentType = "application/json" },
                    Details = new Dictionary<string, object> { { "field", field } }
                };
            }
        }
    }

    private static OpenApiOperation? TryGetOperation(OpenApiDocument doc, string path, OperationType method)
    {
        if (!doc.Paths.TryGetValue(path, out var item)) return null;
        return item.Operations.TryGetValue(method, out var op) ? op : null;
    }

    private static OpenApiSchema? TryGetJsonRequestSchema(OpenApiOperation op)
    {
        if (op.RequestBody == null) return null;

        if (!op.RequestBody.Content.TryGetValue("application/json", out var mediaType))
            return null;

        return mediaType.Schema;
    }

    private static HashSet<string> GetTopLevelRequired(OpenApiSchema? schema)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        if (schema?.Required != null)
        {
            foreach (var r in schema.Required)
                required.Add(r);
        }
        return required;
    }
}
