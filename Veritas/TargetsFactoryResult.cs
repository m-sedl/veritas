using Microsoft.CodeAnalysis.Sarif;

namespace Veritas;

public class TargetsFactoryResult
{
    public List<Target> Targets { get; } = new ();
    public List<Target> ResultsWithoutLocations { get; } = new ();
}