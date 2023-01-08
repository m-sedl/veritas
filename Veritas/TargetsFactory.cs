using Microsoft.CodeAnalysis.Sarif;
using Serilog;
using VSharp;

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

    private readonly ILogger _logger;

    public TargetsFactory(ISequencePointsIndex index, ILogger logger)
    {
        _logger = logger;
        _index = index;
    }

    // maybe should return FactoryResult with targets
    // and sarif issues for which not founded any location
    public TargetsFactoryResult BuildTargets(SarifLog sarif)
    {
        _logger.Information("Started building of targets for report");
        var result = new TargetsFactoryResult();
        var viewedResults = 0;
        var supportedResults = 0;
        foreach (var run in sarif.Runs)
        {
            foreach (var sarifResult in run.Results)
            {
                viewedResults++;
                if (!_supportedRules.ContainsKey(sarifResult.RuleId))
                {
                    _logger.Debug($"Rule {sarifResult.RuleId} not supported, skipped");
                    continue;
                }

                supportedResults++;
                var targets = BuildTargetsByLocations(_supportedRules[sarifResult.RuleId], sarifResult.Locations);
                if (targets.Count == 0)
                {
                    _logger.Debug($"Result ${sarifResult} does not contain any targets");
                    result.BadResults.Add(sarifResult);
                    continue;
                }

                targets.ForEach(t => result.Targets.Add(t));
            }
        }

        _logger.Information($"Targets building completed. Viewed results: {viewedResults}; " +
                            $"Supported results: {supportedResults}; " +
                            $"Supported results without targets: {result.BadResults.Count}; " +
                            $"Total targets: {result.Targets.Count}");
        return result;
    }

    private List<Target> BuildTargetsByLocations(IssueType issue, IList<Location> location)
    {
        // now we can usually get targets for only half of the locations
        var targets = new List<Target>(location.Count / 2);
        foreach (var l in location)
        {
            var points = _index.FindPoints(l.PhysicalLocation);
            switch (points.Count)
            {
                case 0:
                    continue;
                case 1:
                    targets.Add(new Target(issue, points[0].Location, l));
                    break;
                default:
                    var blocks = ResolveBaseBlocks(points);
                    targets.AddRange(blocks.Select(b => new Target(issue, b, l, true)));
                    break;
            }
        }

        return targets;
    }

    private IEnumerable<codeLocation> ResolveBaseBlocks(List<PointInfo> points)
    {
        return points.Select(p =>
        {
            var method = p.Location.method;
            var offset = method.CFG.ResolveBasicBlock(p.Location.offset);
            return new codeLocation(offset, method);
        });
    }
}