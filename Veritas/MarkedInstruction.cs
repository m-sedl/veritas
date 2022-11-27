using Mono.Cecil.Cil;

namespace Veritas;

public class MarkedInstruction
{
    public string DocumentPath { get; }
    public int Offset { get; }
    public int StartLine { get; }
    public int StartColumn { get; }

    public MarkedInstruction(SequencePoint point)
    {
        DocumentPath = point.Document.Url;
        Offset = point.Offset;
        StartLine = point.StartLine;
        StartColumn = point.StartColumn;
    }

    protected bool Equals(MarkedInstruction other)
    {
        return DocumentPath == other.DocumentPath && Offset == other.Offset && StartLine == other.StartLine;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((MarkedInstruction)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DocumentPath, Offset, StartLine);
    }
}