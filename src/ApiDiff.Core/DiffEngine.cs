using ApiDiff.Core.Models;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace ApiDiff.Core;

public class DiffEngine
{
    public IEnumerable<DiffEvent> Compare(string oldJson, string newJson)
    {
        var oldDoc = Parse(oldJson);
        var newDoc = Parse(newJson);

        var oldEndpoints = GetEndpoints(oldDoc);
        var newEndpoints = GetEndpoints(newDoc);
        
        var newEndpointDict = newEndpoints.ToDictionary(e => $"{e.Method} {e.Path}");

        foreach (var oldEndpoint in oldEndpoints)
        {
            var key = $"{oldEndpoint.Method} {oldEndpoint.Path}";

            // Rule #1: Endpoint Removed
            if (!newEndpointDict.TryGetValue(key, out var newEndpoint))
            {
                yield return new DiffEvent($"BREAKING: {key} removed", DiffSeverity.Breaking);
                continue;
            }

            var oldOp = oldEndpoint.Operation!;
            var newOp = newEndpoint.Operation!;

            // Compare Requests
            var requestDiffs = CompareRequests(key, oldOp, newOp);
            foreach (var diff in requestDiffs) yield return diff;

            // Compare Responses
            var responseDiffs = CompareResponses(key, oldOp, newOp);
            foreach (var diff in responseDiffs) yield return diff;
        }
    }

    private IEnumerable<DiffEvent> CompareRequests(string key, OpenApiOperation oldOp, OpenApiOperation newOp)
    {
        var oldSchema = GetJsonSchema(oldOp.RequestBody);
        var newSchema = GetJsonSchema(newOp.RequestBody);

        if (oldSchema == null && newSchema != null)
        {
            // Adding a required request body is breaking if it wasn't there before
            if (newOp.RequestBody.Required)
            {
                yield return new DiffEvent($"BREAKING: {key} added a required request body", DiffSeverity.Breaking);
            }
            yield break;
        }

        if (oldSchema != null && newSchema != null)
        {
            // Rule #2: Request required field added
            var oldRequired = oldSchema.Required ?? new HashSet<string>();
            var newRequired = newSchema.Required ?? new HashSet<string>();

            foreach (var req in newRequired)
            {
                if (!oldRequired.Contains(req))
                {
                    yield return new DiffEvent($"BREAKING: {key} request added required field '{req}'", DiffSeverity.Breaking);
                }
            }

            // Rule #3 & #5: Request field type changed / Enum value removed
            if (oldSchema.Properties != null && newSchema.Properties != null)
            {
                foreach (var prop in oldSchema.Properties)
                {
                    if (newSchema.Properties.TryGetValue(prop.Key, out var newProp))
                    {
                        var oldProp = prop.Value;
                        
                        // Rule #3
                        if (oldProp.Type != newProp.Type)
                        {
                            yield return new DiffEvent($"BREAKING: {key} request field '{prop.Key}' changed type from {oldProp.Type ?? "null"} to {newProp.Type ?? "null"}", DiffSeverity.Breaking);
                        }

                        // Rule #5
                        if (oldProp.Enum != null && oldProp.Enum.Any())
                        {
                            var oldEnums = oldProp.Enum.Select(e => ((Microsoft.OpenApi.Any.OpenApiString)e).Value).ToList();
                            var newEnums = newProp.Enum?.Select(e => ((Microsoft.OpenApi.Any.OpenApiString)e).Value).ToList() ?? new List<string>();

                            foreach (var oldEnum in oldEnums)
                            {
                                if (!newEnums.Contains(oldEnum))
                                {
                                    yield return new DiffEvent($"BREAKING: {key} request field '{prop.Key}' removed enum value '{oldEnum}'", DiffSeverity.Breaking);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private IEnumerable<DiffEvent> CompareResponses(string key, OpenApiOperation oldOp, OpenApiOperation newOp)
    {
        // Typically compare 200/201 (success) responses
        var oldSchema = GetSuccessResponseSchema(oldOp);
        var newSchema = GetSuccessResponseSchema(newOp);

        if (oldSchema != null && newSchema != null)
        {
            if (oldSchema.Properties != null)
            {
                var newProps = newSchema.Properties ?? new Dictionary<string, OpenApiSchema>();

                foreach (var prop in oldSchema.Properties)
                {
                    // Rule #4: Response field removed
                    if (!newProps.TryGetValue(prop.Key, out var newProp))
                    {
                        yield return new DiffEvent($"BREAKING: {key} response removed field '{prop.Key}'", DiffSeverity.Breaking);
                    }
                    else
                    {
                        // Rule #4: Response field type changed
                        if (prop.Value.Type != newProp.Type)
                        {
                            yield return new DiffEvent($"BREAKING: {key} response field '{prop.Key}' changed type from {prop.Value.Type ?? "null"} to {newProp.Type ?? "null"}", DiffSeverity.Breaking);
                        }
                    }
                }
            }
        }
    }

    private OpenApiSchema? GetJsonSchema(OpenApiRequestBody? body)
    {
        if (body?.Content != null && body.Content.TryGetValue("application/json", out var mediaType))
        {
            return mediaType.Schema;
        }
        return null;
    }

    private OpenApiSchema? GetSuccessResponseSchema(OpenApiOperation op)
    {
        if (op.Responses == null) return null;

        var successResponse = op.Responses.FirstOrDefault(r => r.Key.StartsWith("2")).Value;
        if (successResponse?.Content != null && successResponse.Content.TryGetValue("application/json", out var mediaType))
        {
            return mediaType.Schema;
        }
        
        return null;
    }

    private OpenApiDocument Parse(string json)
    {
        var reader = new OpenApiStringReader();
        var document = reader.Read(json, out var diagnostic);
        
        if (document == null)
        {
            var errors = string.Join(", ", diagnostic.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Failed to parse OpenAPI document: {errors}");
        }

        return document;
    }

    private HashSet<Endpoint> GetEndpoints(OpenApiDocument doc)
    {
        var endpoints = new HashSet<Endpoint>();
        if (doc.Paths == null) return endpoints;

        foreach (var path in doc.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                endpoints.Add(new Endpoint(operation.Key.ToString().ToUpperInvariant(), path.Key)
                {
                    Operation = operation.Value
                });
            }
        }
        return endpoints;
    }
}
