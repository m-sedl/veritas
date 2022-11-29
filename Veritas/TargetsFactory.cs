using Microsoft.CodeAnalysis.Sarif;

namespace Veritas;

public class TargetsFactory
{
    private readonly ISequencePointsIndex _index;

    private readonly Dictionary<string, IssueType> _supportedRules = new()
    {
        // https://pvs-studio.com/en/docs/warnings/v3080/
        { "V3080", IssueType.NullDereference },
        // https://pvs-studio.com/en/docs/warnings/v3146/
        { "V3146", IssueType.NullDereference },
        // https://pvs-studio.com/en/docs/warnings/v3106/
        { "V3106", IssueType.OutboundOfRange }
    };

    public TargetsFactory(ISequencePointsIndex index)
    {
        _index = index;
    }

    // maybe should return FactoryResult with targets
    // and sarif issues for which not founded any location
    public TargetsFactoryResult BuildTargets(SarifLog sarif)
    {
        var result = new TargetsFactoryResult();
        foreach (var run in sarif.Runs)
        {
            foreach (var sarifResult in run.Results)
            {
                if (!_supportedRules.ContainsKey(sarifResult.RuleId))
                {
                    //TODO need to add logging
                    continue;
                }

                var targets = BuildTargetsByLocations(_supportedRules[sarifResult.RuleId], sarifResult.Locations);
                if (targets.Count == 0)
                {
                    // TODO need to add logging
                    result.BadResults.Add(sarifResult);
                    continue;
                }
                targets.ForEach(t => result.Targets.Add(t));
            }
        }

        return result;
    }

    private List<Target> BuildTargetsByLocations(IssueType issue, IList<Location> location)
    {
        var targets = new List<Target>(location.Count / 2);
        foreach (var l in location)
        {
            var points = _index.FindInstructions(l.PhysicalLocation);
            switch (points.Count)
            {
                case 0:
                    continue;
                case 1:
                    targets.Add(new Target(issue, points[0].Offset));
                    break;
                default:
                    var blocks = ResolveBaseBlocks(points);
                    targets.AddRange(blocks.Select(b => new Target(issue, b, true)));
                    break;
            }
        }

        return targets;
    }

    private List<int> ResolveBaseBlocks(List<MarkedInstruction> points)
    {
        throw new NotImplementedException();
    }
}