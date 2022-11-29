using Microsoft.CodeAnalysis.Sarif;

namespace Veritas;

public class TargetsFactoryResult
{
    public HashSet<Target> Targets { get; } = new ();
    public List<Result> BadResults { get; } = new ();
}