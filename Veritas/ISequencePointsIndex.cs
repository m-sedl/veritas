using Microsoft.CodeAnalysis.Sarif;
using VSharp;

namespace Veritas;

public interface ISequencePointsIndex
{
    List<PointInfo> FindPoints(PhysicalLocation location);

    PointInfo? FindPoint(codeLocation location);
}