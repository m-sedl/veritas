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
            51,
            6
        };
        yield return new object[]
        {
            "/home/msedlyarskiy/benchmark/projects/btcpayserver/",
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/btcpayserver_btcpayserver.sarif",
            151,
            4
        };
        yield return new object[]
        {
            "/home/msedlyarskiy/benchmark/projects/NLog/",
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/NLog_src_NLog.sarif",
            59,
            101
        };
    }

    [Theory]
    [MemberData(nameof(AnalyzedProjects))]
    public void IndexingQuality(string projectDir, string reportPath, int expectedNotFound, int expectedMultiple)
    {
        var dllPaths = GetAllDlls(projectDir);
        var index = new SequencePointsIndex(dllPaths);
        var report = SarifLog.Load(reportPath);

        var locations = report.Runs
            .SelectMany(run => run.Results.SelectMany(r => r.Locations))
            .Select(l => l.PhysicalLocation);

        var allResults = locations.Select(loc =>
            index.FindPoints(loc)
        ).ToList();

        allResults.ForEach(Assert.NotNull);
        var notFound = allResults.Count(r => r.Count == 0);
        var multiple = allResults.Count(r => r.Count > 1);

        _output.WriteLine($"{notFound}/{allResults.Count} locations not found");
        _output.WriteLine($"{multiple}/{allResults.Count} with multiple points");
        
        Assert.Equal(expectedNotFound, notFound);
        Assert.Equal(expectedMultiple, multiple);
    }
}
