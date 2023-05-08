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
    public void ProveTarget()
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

        var t = factoryResult.Targets.ToList()[3];
        var physicalLocation = t.Result.Locations[0].PhysicalLocation;
        _output.WriteLine($"Target: {t.Result.Message.Text}");
        _output.WriteLine($"Target: {physicalLocation.ArtifactLocation.Uri.AbsolutePath} : {physicalLocation.Region.StartLine}");
        var targets = BuildVSharpTargets(t);

        var statistics = VSharp.TestGenerator.ProveHypotheses(
            targets, 60, 60, testOut.FullName, verbosity: Verbosity.Info, renderTests: false);
        testOut.Delete(true);

        Assert.NotEmpty(statistics.Exceptions);
        var processor = new ExceptionProcessor(index, new List<Target> { t }, statistics.Exceptions);
        var provedTargets = processor.GetProvedTargets();
        Assert.True(provedTargets.Count == 1);
    }
}
