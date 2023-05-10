using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Sarif;
using Serilog;
using VSharp;
using VSharp.Interpreter.IL;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.Ordering;

namespace Veritas.Tests;

[Order(3)]
public class VSharpAnalyzeTests
{
    private readonly ITestOutputHelper _output;

    public VSharpAnalyzeTests(ITestOutputHelper output)
    {
        _output = output;
        AssemblyManager.Reset();
    }

    private List<target> BuildVSharpTargets(Target t)
    {
        return t.Locations.Select(l =>
            new target(t.Issue, l.Location, l.IsBasicBlock)
        ).ToList();
    }

    [Fact]
    public void ProveSingleTarget()
    {
        var reportPath = "/home/msedlyarskiy/benchmark/tools/reports/pvs/litedb_LiteDB.sarif";
        var testOut = new DirectoryInfo("/home/msedlyarskiy/Veritas/Veritas.Tests/test_out");
        var binDirs = new[]
        {
            "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Stress/bin/Debug/netcoreapp3.1/publish",
            "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Tests/bin/Debug/netcoreapp3.1/publish",
            "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Benchmarks/bin/Debug/netcoreapp3.1/publish",
            "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB/bin/Debug/netstandard2.0/publish",
            "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Shell/bin/Debug/netcoreapp3.1/publish"
        };

        var dllPaths = binDirs.SelectMany(Utils.GetAllDlls).ToList();
        var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        var index = new SequencePointsIndex(dllPaths, logger);
        var report = SarifLog.Load(reportPath);

        var factory = new TargetsFactory(index, logger);
        var factoryResult = factory.BuildTargets(report);

        var targetIdx = 3;
        var t = factoryResult.Targets.ToList()[targetIdx];
        var physicalLocation = t.Result.Locations[0].PhysicalLocation;
        _output.WriteLine($"Target: {t.Result.Message.Text}");
        _output.WriteLine($"Target: {physicalLocation.ArtifactLocation.Uri.AbsolutePath} : {physicalLocation.Region.StartLine}");
        var targets = BuildVSharpTargets(t);

        var statistics = VSharp.TestGenerator.ProveHypotheses(
            targets, 60, 60, testOut.FullName, verbosity: Verbosity.Info, renderTests: false);
        testOut.Delete(true);

        Assert.NotEmpty(statistics.Exceptions);
        var processor = new ExceptionProcessor(index, statistics.Exceptions);
        var procTargets = new List<Target> { t };
        procTargets.AddRange(factoryResult.ResultsWithoutLocations);
        var provedTargets = processor.GetProvedTargets(procTargets);
        _output.WriteLine($"Target {targetIdx}");
        _output.WriteLine($"\tProved targets: {provedTargets.Count}");
        Assert.True(provedTargets.Count == 1);
    }

    [Theory]
    [MemberData(nameof(AnalyzedProjects))]
    public void ProvingQuality(string[] binDirs, string reportPath)
    {
        var testOut = new DirectoryInfo("/home/msedlyarskiy/Veritas/Veritas.Tests/test_out");
        var dllPaths = binDirs.SelectMany(Utils.GetAllDlls).ToList();
        dllPaths.Reverse();
        var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        _output.WriteLine("Start indexing");
        var index = new SequencePointsIndex(dllPaths, logger);
        var report = SarifLog.Load(reportPath);

        var factory = new TargetsFactory(index, logger);
        var factoryResult = factory.BuildTargets(report);

        _output.WriteLine("Start ProveHypotheses");
        var provedTargets = new Dictionary<Target, List<ExceptionInfo>>();
        var exceptions = new HashSet<exceptionInfo>();
        for (var i = 0; i < factoryResult.Targets.Count; i++)
        {
            _output.WriteLine($"Target {i}");
            var t = factoryResult.Targets[i];
            var targets = BuildVSharpTargets(t);
            try
            {
                var statistics = VSharp.TestGenerator.ProveHypotheses(
                    targets, 60, 60, testOut.FullName, verbosity: Verbosity.Info);
                statistics.Exceptions.ToList().ForEach(e => exceptions.Add(e));

                var processor = new ExceptionProcessor(index, statistics.Exceptions);

                var pts = processor.GetProvedTargets(new List<Target> { t });
                _output.WriteLine($"\tProved targets: {pts.Count}");
                foreach (var kv in pts)
                {
                    provedTargets[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Exception caught: {ex.Message}");
            }
            finally
            {
                testOut.Delete(true);
            }
        }
        _output.WriteLine($"Total supported results: {TargetsFactory.GetSupportedResultsCount(report)}");
        _output.WriteLine($"Results without targets: {factoryResult.ResultsWithoutLocations.Count}");
        _output.WriteLine($"Founded exceptions: {exceptions.Count}");
        _output.WriteLine($"Proved results: {provedTargets.Count}");
    }

    public static IEnumerable<object[]> AnalyzedProjects()
    {
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Stress/bin/Debug/netcoreapp3.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Tests/bin/Debug/netcoreapp3.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Benchmarks/bin/Debug/netcoreapp3.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/litedb/LiteDB.Shell/bin/Debug/netcoreapp3.1/publish"
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/litedb_LiteDB.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog.Database/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog.WindowsEventLog/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog.OutputDebugString/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/src/NLog.WindowsRegistry/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/tests/NLog.WindowsRegistry.Tests/bin/Debug/netcoreapp3.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/tests/NLogAutoLoadExtension/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/tests/SampleExtensions/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/tests/NLog.UnitTests/bin/Debug/netcoreapp3.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/tests/NLog.Database.Tests/bin/Debug/netcoreapp3.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/tests/ManuallyLoadedExtension/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/NLog/tests/PackageLoaderTestAssembly/bin/Debug/netstandard2.0/publish",
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/NLog_src_NLog.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Tests/bin/Debug/net6.0/linux-x64/publish/",
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Client/bin/Debug/netstandard2.1/linux-x64/publish",
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer/bin/Debug/net6.0/linux-x64/publish",
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Common/bin/Debug/net6.0/linux-x64/publish",
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/Plugins/BTCPayServer.Plugins.Custodians.FakeCustodian/bin/Debug/net6.0/linux-x64/publish",
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Plugins.Test/bin/Debug/net6.0/linux-x64/publish",
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Abstractions/bin/Debug/net6.0/linux-x64/publish",
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.PluginPacker/bin/Debug/net6.0/linux-x64/publish",
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Data/bin/Debug/net6.0/linux-x64/publish",
        //         "/home/msedlyarskiy/benchmark/projects/btcpayserver/BTCPayServer.Rating/bin/Debug/net6.0/linux-x64/publish"
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/btcpayserver_btcpayserver.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/moq4/src/Moq/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/moq4/tests/Moq.Tests.FSharpTypes/bin/Debug/netcoreapp3.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/moq4/tests/Moq.Tests.FSharpTypes/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/moq4/tests/Moq.Tests.VisualBasic/bin/Debug/netcoreapp3.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/moq4/tests/Moq.Tests.VisualBasic/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/moq4/tests/Moq.Tests/bin/Debug/netcoreapp3.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/moq4/tests/Moq.Tests/bin/Debug/net6.0/publish",
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/moq4_Moq.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/nunit/bin/Debug/net6.0",
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/nunit_nunit.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.runner.tdnet.tests/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.runner.inproc.console/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.runner.inproc.console.tests/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.core/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.common/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.runner.utility/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.core.tests/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.common.tests/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.runner.tdnet/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.runner.common/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.assert.tests/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.assert/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.runner.common.tests/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/xunit/src/xunit.v3.runner.utility.tests/bin/Debug/net6.0/publish"
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/xunit.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/AutoMapper/src/IntegrationTests/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/AutoMapper/src/AutoMapper/bin/Debug/netstandard2.1/publish",
        //         "/home/msedlyarskiy/benchmark/projects/AutoMapper/src/UnitTests/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/AutoMapper/src/Benchmark/bin/Debug/net6.0/publish",
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/AutoMapper_AutoMapper.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/spbu-homeworks-1/Semester2/Homework8/BTree.Tests/bin/Debug/net5.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/spbu-homeworks-1/Semester2/Homework8/BTree/bin/Debug/net5.0/publish"
        //
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/spbu-homeworks-1_Semester2_Homework8_BTree_BTree.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/ILSpy/ICSharpCode.ILSpyCmd/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/ILSpy/ICSharpCode.Decompiler/bin/Debug/netstandard2.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/ILSpy/ICSharpCode.ILSpyX/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/ILSpy/ICSharpCode.Decompiler.TestRunner/bin/Debug/net6.0-windows/publish",
        //         "/home/msedlyarskiy/benchmark/projects/ILSpy/ICSharpCode.Decompiler.PowerShell/bin/Debug/netstandard2.0/publish"
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/ILSpy.sarif"
        // };
        yield return new object[]
        {
            new[]
            {
                "/home/msedlyarskiy/benchmark/projects/OpenRA/bin/publish"
            },
            "/home/msedlyarskiy/benchmark/tools/reports/pvs/OpenRA.sarif"
        };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "/home/msedlyarskiy/benchmark/projects/Newtonsoft.Json/Src/Newtonsoft.Json/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/Newtonsoft.Json/Src/Newtonsoft.Json.Tests/bin/Debug/net6.0/publish",
        //         "/home/msedlyarskiy/benchmark/projects/Newtonsoft.Json/Src/Newtonsoft.Json.TestConsole/bin/Debug/net6.0/publish"
        //     },
        //     "/home/msedlyarskiy/benchmark/tools/reports/pvs/Newtonsoft.Json_Src_Newtonsoft.Json.sarif"
        // };
    }
}
