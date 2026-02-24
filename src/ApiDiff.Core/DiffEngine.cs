using ApiDiff.Core.Models;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace ApiDiff.Core;

public class DiffEngine
{
    private readonly List<Interfaces.IApiDiffRule> _rules;

    public DiffEngine()
    {
        _rules = new List<Interfaces.IApiDiffRule>
        {
            new Rules.EndpointRemovedRule(),
            new Rules.RequestBodyAddedRule(),
            new Rules.RequiredRequestFieldAddedRule(),
            new Rules.RequestFieldTypeChangedRule(),
            new Rules.RequestEnumValueRemovedRule(),
            new Rules.ResponseFieldChangedRule()
        };
    }

    public IEnumerable<DiffEvent> Compare(string oldJson, string newJson)
    {
        var oldDoc = Parse(oldJson);
        var newDoc = Parse(newJson);

        var oldEndpoints = GetEndpoints(oldDoc);
        var newEndpoints = GetEndpoints(newDoc);
        
        var newEndpointDict = newEndpoints.ToDictionary(e => $"{e.Method} {e.Path}");
        var commonOperations = new List<(string Path, OperationType Method, OpenApiOperation OldOp, OpenApiOperation NewOp)>();

        foreach (var oldEndpoint in oldEndpoints)
        {
            if (!newEndpointDict.TryGetValue($"{oldEndpoint.Method} {oldEndpoint.Path}", out var newEndpoint))
                continue;

            var oldOp = oldEndpoint.Operation!;
            var newOp = newEndpoint.Operation!;

            var methodMap = new Dictionary<string, OperationType>(StringComparer.OrdinalIgnoreCase)
            {
                { "GET", OperationType.Get },
                { "PUT", OperationType.Put },
                { "POST", OperationType.Post },
                { "DELETE", OperationType.Delete },
                { "OPTIONS", OperationType.Options },
                { "HEAD", OperationType.Head },
                { "PATCH", OperationType.Patch },
                { "TRACE", OperationType.Trace }
            };

            if (methodMap.TryGetValue(oldEndpoint.Method, out var operationType))
            {
                commonOperations.Add((oldEndpoint.Path, operationType, oldOp, newOp));
            }
        }

        var context = new DiffContext
        {
            OldDoc = oldDoc,
            NewDoc = newDoc,
            OldEndpoints = oldEndpoints.ToList(),
            NewEndpoints = newEndpoints.ToList(),
            CommonOperations = commonOperations
        };

        var results = new List<DiffEvent>();

        foreach (var rule in _rules)
        {
            results.AddRange(rule.Evaluate(context));
        }

        foreach (var diff in results.OrderBy(r => r.Message, StringComparer.OrdinalIgnoreCase))
        {
            yield return diff;
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
