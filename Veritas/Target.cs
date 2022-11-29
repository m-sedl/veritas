namespace Veritas;

public class Target
{
    public IssueType Issue { get; }

    public bool IsBaseBlock { get; }

    public int Offset { get; }

    public Target(IssueType issue, int offset, bool isBaseBlock = false)
    {
        Issue = issue;
        Offset = offset;
        IsBaseBlock = isBaseBlock;
    }

    protected bool Equals(Target other)
    {
        return Issue == other.Issue && IsBaseBlock == other.IsBaseBlock && Offset == other.Offset;
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
        return HashCode.Combine((int)Issue, IsBaseBlock, Offset);
    }
}