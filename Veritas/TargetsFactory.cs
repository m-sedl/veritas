
using Microsoft.CodeAnalysis.Sarif;
using Serilog;
using VSharp;
using VSharp.Interpreter.IL;

namespace Veritas;

public class TargetsFactory
{
    private readonly ISequencePointsIndex _index;

    private readonly Dictionary<string, hypothesisType> _supportedRules = new()
    {
        // https://pvs-studio.com/en/docs/warnings/v3080/
        { "V3080", hypothesisType.NullDereference },
        // https://pvs-studio.com/en/docs/warnings/v3146/
        { "V3146", hypothesisType.NullDereference },
        // https://pvs-studio.com/en/docs/warnings/v3106/
        { "V3106", hypothesisType.IndexOutOfRange }
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
                var target = BuildTargetByResult(sarifResult);
                if (target.Locations.Count == 0)
                {
                    _logger.Debug($"The result ${sarifResult} could not be mapped to the target: " +
                                  $"no locations found in the code");
                    result.ResultsWithoutTargets.Add(sarifResult);
                    continue;
                }

                result.Targets.Add(target);
            }
        }

        _logger.Information($"Targets building completed. Viewed results: {viewedResults}; " +
                            $"Supported results: {supportedResults}; " +
                            $"Supported results without targets: {result.ResultsWithoutTargets.Count}; " +
                            $"Total targets: {result.Targets.Count}");
        return result;
    }

    private Target BuildTargetByResult(Result sarifResult)
    {
        // now we can usually get targets for only half of the locations
        var locationsOrBlocks = new HashSet<InstructionOrBlock>(sarifResult.Locations.Count / 2);
        foreach (var l in sarifResult.Locations)
        {
            var points = _index.FindPoints(l.PhysicalLocation);
            switch (points.Count)
            {
                case 0:
                    continue;
                case 1:
                    locationsOrBlocks.Add(new InstructionOrBlock(false, points[0].Location));
                    break;
                default:
                    var blocks = ResolveBasicBlocks(points);
                    foreach (var block in blocks)
                    {
                        locationsOrBlocks.Add(new InstructionOrBlock(true, block));
                    }
                    break;
            }
        }

        return new Target(_supportedRules[sarifResult.RuleId], sarifResult, locationsOrBlocks);
    }

    private IEnumerable<codeLocation> ResolveBasicBlocks(List<PointInfo> points)
    {
        return points.Select(p =>
        {
            var method = p.Location.method;
            var offset = method.ForceCFG.ResolveBasicBlock(p.Location.offset);
            return new codeLocation(offset, method);
        });
    }
}