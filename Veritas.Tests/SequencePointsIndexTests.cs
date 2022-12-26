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

    static IEnumerable<string> GetAllDlls(string path)
    {
        var dir = new DirectoryInfo(path);
        return dir
            .GetFiles("*.dll", SearchOption.AllDirectories)
            .Select(f => f.FullName);
    }

    public static IEnumerable<object[]> AnalyzedProjects()
    {
        yield return new object[]
        {
            new[]
            {
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Tests/bin/Debug/net6.0/linux-x64/publish/",
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Client/bin/Debug/netstandard2.1/linux-x64/publish",
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer/bin/Debug/net6.0/linux-x64/publish",
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Common/bin/Debug/net6.0/linux-x64/publish",
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/Plugins/BTCPayServer.Plugins.Custodians.FakeCustodian/bin/Debug/net6.0/linux-x64/publish",
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Plugins.Test/bin/Debug/net6.0/linux-x64/publish",
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Abstractions/bin/Debug/net6.0/linux-x64/publish",
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.PluginPacker/bin/Debug/net6.0/linux-x64/publish",
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Data/bin/Debug/net6.0/linux-x64/publish",
                "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Rating/bin/Debug/net6.0/linux-x64/publish"
            },
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/btcpayserver_btcpayserver.sarif",
            155,
            4
        };
        yield return new object[]
        {
            new[]
            {
                "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Stress/bin/Debug/netcoreapp3.1/publish",
                "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Tests/bin/Debug/netcoreapp3.1/publish",
                "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Benchmarks/bin/Debug/netcoreapp3.1/publish",
                "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB/bin/Debug/netstandard2.0/publish",
                "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Shell/bin/Debug/netcoreapp3.1/publish"
            },
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/litedb_LiteDB.sarif",
            51,
            3
        };
        yield return new object[]
        {
            new[]
            {
                "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog.Database/bin/Debug/netstandard2.0/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog/bin/Debug/netstandard2.0/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog.WindowsEventLog/bin/Debug/netstandard2.0/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog.OutputDebugString/bin/Debug/netstandard2.0/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog.WindowsRegistry/bin/Debug/netstandard2.0/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/tests/NLog.WindowsRegistry.Tests/bin/Debug/netcoreapp3.1/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/tests/NLogAutoLoadExtension/bin/Debug/netstandard2.0/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/tests/SampleExtensions/bin/Debug/netstandard2.0/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/tests/NLog.UnitTests/bin/Debug/netcoreapp3.1/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/tests/NLog.Database.Tests/bin/Debug/netcoreapp3.1/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/tests/ManuallyLoadedExtension/bin/Debug/netstandard2.0/publish",
                "/home/msedlyarskiy/benchmark/projects/NLog/tests/PackageLoaderTestAssembly/bin/Debug/netstandard2.0/publish",
            },
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/NLog_src_NLog.sarif",
            59,
            5
        };
    }

    [Theory]
    [MemberData(nameof(AnalyzedProjects))]
    public void IndexingQuality(string[] binDirs, string reportPath, int expectedNotFound, int expectedMultiple)
    {
        var dllPaths = binDirs.SelectMany(GetAllDlls).ToList();
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
