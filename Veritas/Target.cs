using Microsoft.CodeAnalysis.Sarif;
using VSharp;
using VSharp.Interpreter.IL;

namespace Veritas;

public class Target
{
    public hypothesisType Issue { get; }
   
    public Result Result { get; }
    
    public HashSet<InstructionOrBlock> Locations { get; }

    public Target(hypothesisType issue, Result result, HashSet<InstructionOrBlock> locations)
    {
        Issue = issue;
        Result = result;
        Locations = locations;
    }

    public override string ToString()
    {
        var locations = string.Join("\t\n", Locations.Select(l => l.ToString()));
        return $"{Issue}\n\t{locations}";
    }
}

public class InstructionOrBlock
{
    public bool IsBasicBlock { get; }
    
    public codeLocation Location { get; }

    public InstructionOrBlock(bool isBasicBlock, codeLocation location)
    {
        IsBasicBlock = isBasicBlock;
        Location = location;
    }

    protected bool Equals(InstructionOrBlock other)
    {
        return IsBasicBlock == other.IsBasicBlock && Location.Equals(other.Location);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((InstructionOrBlock)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(IsBasicBlock, Location);
    }
    
    public override string ToString()
    {
        var type = IsBasicBlock ? "Basic block" : "Instruction";
        var method = Location.method.FullName;
        return $"{type} {method} 0x{Location.offset:X}";
    }
}