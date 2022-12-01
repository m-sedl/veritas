using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Sarif;
using Xunit;
using Xunit.Abstractions;

namespace Veritas.Tests;

public class TargetsFactoryTests
{
    private readonly ITestOutputHelper _output;
    
    public TargetsFactoryTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    static List<string> GetAllDlls(string path)
    {
        var dir = new DirectoryInfo(path);
        return dir
            .GetFiles("*.dll", SearchOption.AllDirectories)
            .Select(f => f.FullName)
            .Where(f => !f.Contains("/obj/"))
            .ToList();
    }
    
    public static IEnumerable<object[]> AnalyzedProjects()
    {
        yield return new object[]
        {
            "/home/msedlyarskiy/benchmark/projects/litedb/",
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/litedb_LiteDB.sarif",
            14,
            8
        };
        yield return new object[]
        {
            "/home/msedlyarskiy/benchmark/projects/NLog/",
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/NLog_src_NLog.sarif",
            0,
            0
        };
        yield return new object[]
        {
            "/home/msedlyarskiy/benchmark/projects/btcpayserver/",
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/btcpayserver_btcpayserver.sarif",
            68,
            44
        };
    }

    [Theory]
    [MemberData(nameof(AnalyzedProjects))]
    public void BuildTargetsTest(string projectDir, string reportPath, int expectedTargets, int expectedBadResults)
    {
        var dllPaths = GetAllDlls(projectDir);
        var index = new SequencePointsIndex(dllPaths);
        var report = SarifLog.Load(reportPath);

        var factory = new TargetsFactory(index);
        var result = factory.BuildTargets(report);
        var baseBlocks = result.Targets.Count(t => t.IsBaseBlock);

        _output.WriteLine($"Targets: {result.Targets.Count}");
        _output.WriteLine($"Bad results: {result.BadResults.Count}");
        _output.WriteLine($"Base blocks: {baseBlocks}");
        _output.WriteLine("");

        foreach (var t in result.Targets)
        {
            _output.WriteLine($"{t}\n-------");
        }

        Assert.Equal(expectedBadResults, result.BadResults.Count);
        Assert.Equal(expectedTargets, result.Targets.Count);
    }
}