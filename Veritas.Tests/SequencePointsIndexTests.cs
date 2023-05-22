using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Sarif;
using Serilog;
using VSharp;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.Ordering;

namespace Veritas.Tests;

[Order(1)]
public class SequencePointsIndexTests
{
    private readonly ITestOutputHelper _output;

    public SequencePointsIndexTests(ITestOutputHelper output)
    {
        _output = output;
        AssemblyManager.Reset();
    }

    public static IEnumerable<object[]> AnalyzedProjects()
    {
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/btcpayserver/BTCPayServer.Tests/bin/Debug/net6.0/linux-x64/publish/",
                "../../../../../benchmark/projects/btcpayserver/BTCPayServer.Client/bin/Debug/netstandard2.1/linux-x64/publish",
                "../../../../../benchmark/projects/btcpayserver/BTCPayServer/bin/Debug/net6.0/linux-x64/publish",
                "../../../../../benchmark/projects/btcpayserver/BTCPayServer.Common/bin/Debug/net6.0/linux-x64/publish",
                "../../../../../benchmark/projects/btcpayserver/Plugins/BTCPayServer.Plugins.Custodians.FakeCustodian/bin/Debug/net6.0/linux-x64/publish",
                "../../../../../benchmark/projects/btcpayserver/BTCPayServer.Plugins.Test/bin/Debug/net6.0/linux-x64/publish",
                "../../../../../benchmark/projects/btcpayserver/BTCPayServer.Abstractions/bin/Debug/net6.0/linux-x64/publish",
                "../../../../../benchmark/projects/btcpayserver/BTCPayServer.PluginPacker/bin/Debug/net6.0/linux-x64/publish",
                "../../../../../benchmark/projects/btcpayserver/BTCPayServer.Data/bin/Debug/net6.0/linux-x64/publish",
                "../../../../../benchmark/projects/btcpayserver/BTCPayServer.Rating/bin/Debug/net6.0/linux-x64/publish"
            },
            "../../../../../benchmark/tools/reports/pvs/btcpayserver_btcpayserver.sarif",
            26,
            352
        };
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/litedb/LiteDB.Stress/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/litedb/LiteDB.Tests/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/litedb/LiteDB.Benchmarks/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/litedb/LiteDB/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/litedb/LiteDB.Shell/bin/Debug/netcoreapp3.1/publish"
            },
            "../../../../../benchmark/tools/reports/pvs/litedb_LiteDB.sarif",
            0,
            110
        };
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/NLog/src/NLog.Database/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/NLog/src/NLog/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/NLog/src/NLog.WindowsEventLog/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/NLog/src/NLog.OutputDebugString/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/NLog/src/NLog.WindowsRegistry/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/NLog/tests/NLog.WindowsRegistry.Tests/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/NLog/tests/NLogAutoLoadExtension/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/NLog/tests/SampleExtensions/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/NLog/tests/NLog.UnitTests/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/NLog/tests/NLog.Database.Tests/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/NLog/tests/ManuallyLoadedExtension/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/NLog/tests/PackageLoaderTestAssembly/bin/Debug/netstandard2.0/publish",
            },
            "../../../../../benchmark/tools/reports/pvs/NLog_src_NLog.sarif",
            9,
            154
        };
    }

    [Theory]
    [MemberData(nameof(AnalyzedProjects))]
    public void IndexingQuality(string[] binDirs, string reportPath, int expectedNotFound, int expectedMultiple)
    {
        var dllPaths = binDirs.SelectMany(Utils.GetAllDlls).ToList();
        var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        var index = new SequencePointsIndex(dllPaths, logger);
        var report = SarifLog.Load(reportPath);

        var locations = report.Runs
            .SelectMany(run => run.Results.SelectMany(r => r.Locations))
            .Select(l => l.PhysicalLocation);

        var allResults = locations.Select(loc =>
            index.FindPoints(loc)
        ).ToList();

        var loc = allResults[0][0].Location;
        var point = index.FindPoint(loc);
        Assert.NotNull(point);
        Assert.Equal(loc, point.Location);

        allResults.ForEach(Assert.NotNull);
        var notFound = allResults.Count(r => r.Count == 0);
        var multiple = allResults.Count(r => r.Count > 1);

        _output.WriteLine($"{notFound}/{allResults.Count} locations not found");
        _output.WriteLine($"{multiple}/{allResults.Count} with multiple points");

        Assert.Equal(expectedNotFound, notFound);
        Assert.Equal(expectedMultiple, multiple);
    }

    [Theory]
    [InlineData("../../../../../benchmark/tools/reports/pvs/litedb_LiteDB.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/NLog_src_NLog.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/btcpayserver_btcpayserver.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/moq4_Moq.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/nunit_nunit.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/xunit.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/AutoMapper_AutoMapper.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/spbu-homeworks-1_Semester2_Homework8_BTree_BTree.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/ILSpy.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/OpenRA.sarif")]
    [InlineData("../../../../../benchmark/tools/reports/pvs/Newtonsoft.Json_Src_Newtonsoft.Json.sarif")]
    public void LoadSarif(string path)
    {
        var report = SarifLog.Load(path);
        var v3080 = report.Runs.SelectMany(run => run.Results).Count(sarifResult => sarifResult.RuleId == "V3080");
        var v3146 = report.Runs.SelectMany(run => run.Results).Count(sarifResult => sarifResult.RuleId == "V3146");
        var v3106 = report.Runs.SelectMany(run => run.Results).Count(sarifResult => sarifResult.RuleId == "V3106");
        var v3022 = report.Runs.SelectMany(run => run.Results).Count(sarifResult => sarifResult.RuleId == "V3022");
        _output.WriteLine(path.Split("/").Last());
        _output.WriteLine($"V3022: {v3022}");
        _output.WriteLine($"V3080: {v3080}");
        _output.WriteLine($"V3146: {v3146}");
        _output.WriteLine($"V3106: {v3106}");
        _output.WriteLine($"Total: {report.Runs.SelectMany(run => run.Results).Count()}");
    }
}
