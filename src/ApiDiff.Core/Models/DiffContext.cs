using Microsoft.OpenApi.Models;

namespace ApiDiff.Core.Models;

public class DiffContext
{
    public OpenApiDocument OldDoc { get; set; } = null!;
    public OpenApiDocument NewDoc { get; set; } = null!;
    
    public IReadOnlyList<Endpoint> OldEndpoints { get; set; } = null!;
    public IReadOnlyList<Endpoint> NewEndpoints { get; set; } = null!;
    
    public IReadOnlyList<(string Path, OperationType Method, OpenApiOperation OldOp, OpenApiOperation NewOp)> CommonOperations { get; set; } = null!;
}
