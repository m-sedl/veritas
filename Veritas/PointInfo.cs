using Mono.Cecil.Cil;
using VSharp;

namespace Veritas;

public class PointInfo
{
    public codeLocation Location { get; }

    public int StartLine { get; }

    public int StartColumn { get; }

    public PointInfo(SequencePoint point, Method method)
    {
        Location = new codeLocation(point.Offset, method);
        StartLine = point.StartLine;
        StartColumn = point.StartColumn;
    }

    protected bool Equals(PointInfo other)
    {
        return Location.Equals(other.Location) && StartLine == other.StartLine && StartColumn == other.StartColumn;
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
        return HashCode.Combine(Location, StartLine, StartColumn);
    }

    public override string ToString()
    {
        return $"{StartLine} {Location}";
    }
}