using Microsoft.OpenApi.Models;

namespace ApiDiff.Core.Models;

public record Endpoint(string Method, string Path)
{
    public OpenApiOperation? Operation { get; set; }
}
