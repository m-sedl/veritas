
using Microsoft.CodeAnalysis.Sarif;
using Serilog;
using VSharp;
using VSharp.Interpreter.IL;

namespace Veritas;

public class TargetsFactory
{
    private readonly ISequencePointsIndex _index;

    private static readonly Dictionary<string, hypothesisType> SupportedRules = new()
    {
        // https://pvs-studio.com/en/docs/warnings/v3080/
        { "V3080", hypothesisType.NullDereference },
        // https://pvs-studio.com/en/docs/warnings/v3146/
        { "V3146", hypothesisType.NullDereference },
        // https://pvs-studio.com/en/docs/warnings/v3106/
        { "V3106", hypothesisType.IndexOutOfRange }
    };

    private readonly ILogger _logger;

    public static int GetSupportedResultsCount(SarifLog sarif)
    {
        return sarif
            .Runs.SelectMany(run => run.Results)
            .Count(sarifResult => SupportedRules.ContainsKey(sarifResult.RuleId));
    }

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
                if (!SupportedRules.ContainsKey(sarifResult.RuleId))
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
                    result.ResultsWithoutLocations.Add(target);
                    continue;
                }

                result.Targets.Add(target);
            }
        }

        _logger.Information($"Targets building completed. Viewed results: {viewedResults}; " +
                            $"Supported results: {supportedResults}; " +
                            $"Supported results without targets: {result.ResultsWithoutLocations.Count}; " +
                            $"Total targets: {result.Targets.Count}");
        return result;
    }

    private Target BuildTargetByResult(Result sarifResult)
    {
        var locationsOrBlocks = new HashSet<InstructionOrBlock>(sarifResult.Locations.Count);
        foreach (var l in sarifResult.Locations)
        {
            var points = _index.FindPoints(l.PhysicalLocation);
            foreach (var p in points)
            {
                locationsOrBlocks.Add(new InstructionOrBlock(false, p.Location));
            }
        }

        return new Target(SupportedRules[sarifResult.RuleId], sarifResult, locationsOrBlocks);
    }
}