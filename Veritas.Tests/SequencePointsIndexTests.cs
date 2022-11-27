using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Sarif;
using Xunit;
using Xunit.Abstractions;

namespace Veritas.Tests;

public class SequencePointsIndexTests
{
    private readonly ITestOutputHelper _output;

    public SequencePointsIndexTests(ITestOutputHelper output)
    {
        _output = output;
    }

    static List<string> GetAllDlls(string path)
    {
        var dir = new DirectoryInfo(path);
        return dir.GetFiles("*.dll", SearchOption.AllDirectories).Select(f => f.FullName).ToList();
    }

    public static IEnumerable<object[]> AnalyzedProjects()
    {
        yield return new object[]
        {
            "/home/msedlyarskiy/benchmark/projects/litedb/",
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/litedb_LiteDB.sarif"
        };
        yield return new object[]
        {
            "/home/msedlyarskiy/benchmark/projects/btcpayserver/",
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/btcpayserver_btcpayserver.sarif"
        };
        yield return new object[]
        {
            "/home/msedlyarskiy/benchmark/projects/NLog/",
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/NLog_src_NLog.sarif"
        };
    }
    
    [Theory]
    [MemberData(nameof(AnalyzedProjects))]
    public void IndexingQuality(string projectDir, string reportPath)
    {
        var dllPaths = GetAllDlls(projectDir);
        var index = new SequencePointsIndex(dllPaths);
        var report = SarifLog.Load(reportPath);

        var locations = report.Runs
            .SelectMany(run => run.Results.SelectMany(r => r.Locations))
            .Select(l => l.PhysicalLocation);

        var allResults = locations.Select(loc =>
            index.FindInstructions(loc.ArtifactLocation.Uri.AbsolutePath, loc.Region.StartLine)
        ).ToList();
        
        allResults.ForEach(Assert.NotNull);
        var emptyResults = allResults.Count(r => r.Count == 0);
        var notFound = emptyResults * 100.0 / allResults.Count;
        _output.WriteLine($"{emptyResults}/{allResults.Count} -- {notFound:0.##}% locations not found");
        Assert.True(notFound < 50.0);
    }
}