using Microsoft.CodeAnalysis.Sarif;

namespace Veritas;

public interface ISequencePointsIndex
{
    List<PointInfo> FindPoints(PhysicalLocation location);
}