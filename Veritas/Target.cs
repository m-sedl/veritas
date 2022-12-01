using Microsoft.CodeAnalysis.Sarif;
using VSharp;

namespace Veritas;

public class Target
{
    public IssueType Issue { get; }

    public bool IsBaseBlock { get; }

    public codeLocation Location { get; }
    
    public Location SarifLocation { get;  }

    public Target(IssueType issue, codeLocation location, Location sarifLocation, bool isBaseBlock = false)
    {
        Issue = issue;
        Location = location;
        SarifLocation = sarifLocation;
        IsBaseBlock = isBaseBlock;
    }

    protected bool Equals(Target other)
    {
        return Issue == other.Issue && IsBaseBlock == other.IsBaseBlock && Location.Equals(other.Location);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Target)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Issue, IsBaseBlock, Location);
    }

    public override string ToString()
    {
        var type = IsBaseBlock ? "Base block" : "Instruction";
        var file = SarifLocation.PhysicalLocation.ArtifactLocation.Uri.AbsolutePath;
        var line = SarifLocation.PhysicalLocation.Region.StartLine;
        var col = SarifLocation.PhysicalLocation.Region.StartColumn;
        var method = Location.method.FullName;
        return $"{Issue}\n{type} 0x{Location.offset:X}\n{method}\n{file} {line}:{col}";
    }
}