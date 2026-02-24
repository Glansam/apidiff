using ApiDiff.Core.Models;

namespace ApiDiff.Core.Interfaces;

public interface IApiDiffRule
{
    IEnumerable<DiffEvent> Evaluate(DiffContext context);
}
