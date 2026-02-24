using ApiDiff.Core.Interfaces;
using ApiDiff.Core.Models;
using Microsoft.OpenApi.Models;

namespace ApiDiff.Core.Rules;

/// <summary>
/// Rule #1: Endpoint Removed
/// Checks if an endpoint that existed in the old specification is entirely missing in the new specification.
/// </summary>
public sealed class EndpointRemovedRule : IApiDiffRule
{
    public IEnumerable<DiffEvent> Evaluate(DiffContext context)
    {
        var newEndpointDict = context.NewEndpoints.ToDictionary(e => $"{e.Method} {e.Path}");

        foreach (var oldEndpoint in context.OldEndpoints)
        {
            var key = $"{oldEndpoint.Method} {oldEndpoint.Path}";
            if (!newEndpointDict.ContainsKey(key))
            {
                yield return new DiffEvent(DiffSeverity.Breaking, "ENDPOINT_REMOVED", $"BREAKING: {key} removed")
                {
                    Operation = new DiffOperation { Method = oldEndpoint.Method, Path = oldEndpoint.Path }
                };
            }
        }
    }
}
