using Mono.Cecil.Cil;
using VSharp;

namespace Veritas;

public class PointInfo
{
    public codeLocation Location { get; }

    public string FileName { get; }

    public int StartLine { get; }

    public int StartColumn { get; }

    public int EndLine { get; }

    public int EndColumn { get; }

    public PointInfo(SequencePoint point, Method method)
    {
        Location = new codeLocation(point.Offset, method);
        FileName = point.Document.Url;
        StartLine = point.StartLine + 1;
        StartColumn = point.StartColumn + 1;
        EndLine = point.EndLine + 1;
        EndColumn = point.EndColumn + 1;
    }

    protected bool Equals(PointInfo other)
    {
        return Location.Equals(other.Location) 
        && StartLine == other.StartLine && StartColumn == other.StartColumn 
        && EndLine == other.EndLine && EndColumn == other.EndColumn;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((PointInfo)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Location, StartLine, StartColumn, EndLine, EndColumn);
    }

    public override string ToString()
    {
        return $"{StartLine}:{StartColumn}--{EndLine}{EndColumn} {Location}";
    }
}