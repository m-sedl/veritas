using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Sarif;
using Serilog;
using VSharp;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.Ordering;

namespace Veritas.Tests;

[Order(2)]
public class TargetsFactoryTests
{
    private readonly ITestOutputHelper _output;

    private readonly Regex serviceSymbols = new Regex("\\{|\\}|\\(|\\)|;");

    public TargetsFactoryTests(ITestOutputHelper output)
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
                "../../../../../benchmark/projects/litedb/LiteDB.Stress/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/litedb/LiteDB.Tests/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/litedb/LiteDB.Benchmarks/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/litedb/LiteDB/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/litedb/LiteDB.Shell/bin/Debug/netcoreapp3.1/publish"
            },
            "../../../../../benchmark/tools/reports/pvs/litedb_LiteDB.sarif",
            19,
            1
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
            1
        };
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
            109,
            5
        };
    }

    [Theory]
    [MemberData(nameof(AnalyzedProjects))]
    public void BuildTargetsTest(string[] binDirs, string reportPath, int expectedTargets, int expectedBadResults)
    {
        var dllPaths = binDirs.SelectMany(Utils.GetAllDlls).ToList();
        var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        var index = new SequencePointsIndex(dllPaths, logger);
        var report = SarifLog.Load(reportPath);

        var factory = new TargetsFactory(index, logger);
        var result = factory.BuildTargets(report);
        _output.WriteLine($"Targets: {result.Targets.Count}");
        _output.WriteLine($"Results without targets: {result.ResultsWithoutLocations.Count}");

        Assert.Equal(expectedBadResults, result.ResultsWithoutLocations.Count);
        Assert.Equal(expectedTargets, result.Targets.Count);
    }

    [Theory]
    [MemberData(nameof(AnalyzedTargets))]
    public void ResultsWithoutTargets(string[] binDirs, string reportPath)
    {
        var dllPaths = binDirs.SelectMany(Utils.GetAllDlls).ToList();
        var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        var index = new SequencePointsIndex(dllPaths, logger);
        var report = SarifLog.Load(reportPath);

        var factory = new TargetsFactory(index, logger);
        var result = factory.BuildTargets(report);

        var resultsWithEmptyLocation = 0;
        _output.WriteLine(reportPath.Split("/").Last());
        foreach (var t in result.ResultsWithoutLocations)
        {
            var location = t.Result.Locations[0].PhysicalLocation;
            var lines = File.ReadAllLines(location.ArtifactLocation.Uri.AbsolutePath);
            var l = lines[location.Region.StartLine - 1];
            var erasedLine = serviceSymbols.Replace(l, "");
            if (String.IsNullOrWhiteSpace(erasedLine) || l.Trim().StartsWith("//") || l.Trim().StartsWith("#"))
            {
                resultsWithEmptyLocation++;
            }
            else
            {
                _output.WriteLine($"{t.Result.RuleId}: \"{l}\"");
            }
        }
        _output.WriteLine($"resultsWithEmptyLocation: {resultsWithEmptyLocation}");
    }

    public static IEnumerable<object[]> AnalyzedTargets()
    {
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
            "../../../../../benchmark/tools/reports/pvs/litedb_LiteDB.sarif"
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
            "../../../../../benchmark/tools/reports/pvs/NLog_src_NLog.sarif"
        };
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
            "../../../../../benchmark/tools/reports/pvs/btcpayserver_btcpayserver.sarif"
        };
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/moq4/src/Moq/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/moq4/tests/Moq.Tests.FSharpTypes/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/moq4/tests/Moq.Tests.FSharpTypes/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/moq4/tests/Moq.Tests.VisualBasic/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/moq4/tests/Moq.Tests.VisualBasic/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/moq4/tests/Moq.Tests/bin/Debug/netcoreapp3.1/publish",
                "../../../../../benchmark/projects/moq4/tests/Moq.Tests/bin/Debug/net6.0/publish",
            },
            "../../../../../benchmark/tools/reports/pvs/moq4_Moq.sarif"
        };
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/nunit/bin/Debug/net6.0",
            },
            "../../../../../benchmark/tools/reports/pvs/nunit_nunit.sarif"
        };
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.tdnet.tests/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.inproc.console/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.inproc.console.tests/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.core/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.common/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.utility/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.core.tests/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.common.tests/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.tdnet/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.common/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.assert.tests/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.assert/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.common.tests/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.utility.tests/bin/Debug/net6.0/publish"
            },
            "../../../../../benchmark/tools/reports/pvs/xunit.sarif"
        };
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/AutoMapper/src/IntegrationTests/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/AutoMapper/src/AutoMapper/bin/Debug/netstandard2.1/publish",
                "../../../../../benchmark/projects/AutoMapper/src/UnitTests/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/AutoMapper/src/Benchmark/bin/Debug/net6.0/publish",
            },
            "../../../../../benchmark/tools/reports/pvs/AutoMapper_AutoMapper.sarif"
        };
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/spbu-homeworks-1/Semester2/Homework8/BTree.Tests/bin/Debug/net5.0/publish",
                "../../../../../benchmark/projects/spbu-homeworks-1/Semester2/Homework8/BTree/bin/Debug/net5.0/publish"

            },
            "../../../../../benchmark/tools/reports/pvs/spbu-homeworks-1_Semester2_Homework8_BTree_BTree.sarif"
        };
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/ILSpy/ICSharpCode.ILSpyCmd/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/ILSpy/ICSharpCode.Decompiler/bin/Debug/netstandard2.0/publish",
                "../../../../../benchmark/projects/ILSpy/ICSharpCode.ILSpyX/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/ILSpy/ICSharpCode.Decompiler.TestRunner/bin/Debug/net6.0-windows/publish",
                "../../../../../benchmark/projects/ILSpy/ICSharpCode.Decompiler.PowerShell/bin/Debug/netstandard2.0/publish"
            },
            "../../../../../benchmark/tools/reports/pvs/ILSpy.sarif"
        };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/OpenRA/bin/publish"
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/OpenRA.sarif"
        // };
        yield return new object[]
        {
            new[]
            {
                "../../../../../benchmark/projects/Newtonsoft.Json/Src/Newtonsoft.Json/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/Newtonsoft.Json/Src/Newtonsoft.Json.Tests/bin/Debug/net6.0/publish",
                "../../../../../benchmark/projects/Newtonsoft.Json/Src/Newtonsoft.Json.TestConsole/bin/Debug/net6.0/publish"
            },
            "../../../../../benchmark/tools/reports/pvs/Newtonsoft.Json_Src_Newtonsoft.Json.sarif"
        };
    }
}