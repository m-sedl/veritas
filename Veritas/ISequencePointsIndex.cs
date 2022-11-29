using Microsoft.CodeAnalysis.Sarif;

namespace Veritas;

public interface ISequencePointsIndex
{
    List<MarkedInstruction> FindInstructions(PhysicalLocation location);
}