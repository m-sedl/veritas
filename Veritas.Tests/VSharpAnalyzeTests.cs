using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
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

    private Serilog.Core.Logger _logger;

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

    private void PrintInfo(string msg)
    {
        _output.WriteLine(msg);
        _logger.Information(msg);
    }

    // [Fact]
    // public void ProveSingleTarget()
    // {
    //     var reportPath = "../../../../../benchmark/tools/reports/pvs/litedb_LiteDB.sarif";
    //     var testOut = new DirectoryInfo("../../../../../Veritas/Veritas.Tests/test_out");
    //     var binDirs = new[]
    //     {
    //         "../../../../../benchmark/projects/litedb/LiteDB.Stress/bin/Debug/netcoreapp3.1/publish",
    //         "../../../../../benchmark/projects/litedb/LiteDB.Tests/bin/Debug/netcoreapp3.1/publish",
    //         "../../../../../benchmark/projects/litedb/LiteDB.Benchmarks/bin/Debug/netcoreapp3.1/publish",
    //         "../../../../../benchmark/projects/litedb/LiteDB/bin/Debug/netstandard2.0/publish",
    //         "../../../../../benchmark/projects/litedb/LiteDB.Shell/bin/Debug/netcoreapp3.1/publish"
    //     };
    //
    //     var dllPaths = binDirs.SelectMany(Utils.GetAllDlls).ToList();
    //     var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
    //     var index = new SequencePointsIndex(dllPaths, logger);
    //     var report = SarifLog.Load(reportPath);
    //
    //     var factory = new TargetsFactory(index, logger);
    //     var factoryResult = factory.BuildTargets(report);
    //
    //     var targetIdx = 3;
    //     var t = factoryResult.Targets.ToList()[targetIdx];
    //     var physicalLocation = t.Result.Locations[0].PhysicalLocation;
    //     _output.WriteLine($"Target: {t.Result.Message.Text}");
    //     _output.WriteLine($"Target: {physicalLocation.ArtifactLocation.Uri.AbsolutePath} : {physicalLocation.Region.StartLine}");
    //     var targets = BuildVSharpTargets(t);
    //
    //     var statistics = VSharp.TestGenerator.ProveHypotheses(
    //         targets, 60, 60, testOut.FullName, verbosity: Verbosity.Info, renderTests: false);
    //     testOut.Delete(true);
    //
    //     Assert.NotEmpty(statistics.Exceptions);
    //     var processor = new ExceptionProcessor(index, statistics.Exceptions);
    //     var procTargets = new List<Target> { t };
    //     procTargets.AddRange(factoryResult.ResultsWithoutLocations);
    //     var provedTargets = processor.GetProvedTargets(procTargets);
    //     _output.WriteLine($"Target {targetIdx}");
    //     _output.WriteLine($"\tProved targets: {provedTargets.Count}");
    //     Assert.True(provedTargets.Count == 1);
    // }

    [Theory]
    [MemberData(nameof(AnalyzedProjects))]
    public void ProvingQuality(string[] binDirs, string reportPath)
    {
        var dllPaths = binDirs.SelectMany(Utils.GetAllDlls).ToList();
        //dllPaths.Reverse();

        var projectName = Path.GetFileNameWithoutExtension(reportPath);
        _logger = new LoggerConfiguration()
            .WriteTo.File($"../../../../../Veritas/Veritas.Tests/{projectName}_log.txt")
            .CreateLogger();

        _output.WriteLine("Start indexing");
        var index = new SequencePointsIndex(dllPaths, _logger);
        var report = SarifLog.Load(reportPath);

        var factory = new TargetsFactory(index, _logger);
        var factoryResult = factory.BuildTargets(report);

        PrintInfo("Start ProveHypotheses");
        var exceptions = new HashSet<Exception>();
        for (var i = 0; i < factoryResult.Targets.Count - 1; i++)
        {
            PrintInfo($"Target {i + 1}/{factoryResult.Targets.Count}");
            var t = factoryResult.Targets[i];
            var oldExceptionCount = exceptions.Count;
            FindExceptionsForTarget(t, exceptions);
            PrintInfo($"new exceptions added: {exceptions.Count - oldExceptionCount}");
        }

        PrintInfo($"Start TryProveTargets");
        var provedTargets = TryProveTargets(factoryResult.Targets, exceptions);

        PrintInfo($"Total supported results: {TargetsFactory.GetSupportedResultsCount(report)}");
        PrintInfo($"Results without targets: {factoryResult.ResultsWithoutLocations.Count}");
        PrintInfo($"Founded exceptions: {exceptions.Count}");
        PrintInfo($"Proved results: {provedTargets.Count}");


        foreach (var kv in provedTargets) 
        {
            var r = kv.Key.Result;
            PrintInfo(r.Message.Text);
            var path = r.Locations[0].PhysicalLocation.ArtifactLocation.Uri.AbsolutePath;
            var startLine = r.Locations[0].PhysicalLocation.Region.StartLine;
            PrintInfo($"{path}, {startLine}");
            PrintInfo($"Has exceptions: {kv.Value.Count}");

            foreach (var ex in kv.Value) 
            {
                PrintInfo(ex.Message + "\n" + ex.StackTrace);
                PrintInfo("-----");
            }
            PrintInfo("******");
        }

        PrintInfo("==========");
    }

    private void FindExceptionsForTarget(Target t, HashSet<Exception> exceptions)
    {
        var testOut = new DirectoryInfo("../../../../../Veritas/Veritas.Tests/test_out");
        var resultPath = Path.Combine(testOut.FullName, "VSharp.tests.0");
        var timeout = 60;
        var targets = BuildVSharpTargets(t);
        try
        {
            VSharp.TestGenerator.ProveHypotheses(targets, timeout, timeout, testOut.FullName, verbosity: Verbosity.Error);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Exception caught: {ex.Message}");
        }
        finally
        {
            PrintInfo("reproducing errors...");
            var tests = Directory.EnumerateFiles(resultPath, "*.vst").Select(p => new FileInfo(p)).ToList();
            ReproduceErrors(tests).ForEach(e => exceptions.Add(e));
            testOut.Delete(true);
        }
    }

    private List<Exception> ReproduceErrors(List<FileInfo> tests)
    {
        var exceptions = new List<Exception>();
        try
        {
            foreach (var fileInfo in tests)
            {
                testInfo ti;
                using (FileStream stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read))
                {
                    ti = UnitTest.DeserializeTestInfo(stream);
                }
                UnitTest test = UnitTest.DeserializeFromTestInfo(ti, false);
                if (!test.IsError)
                {
                    continue;
                }

                var ex = RunAndTryCatch(test);
                if (ex != null)
                {
                    exceptions.Add(ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Reproducing errors failed: {ex.Message} {ex.StackTrace}");
        }
        return exceptions;
    }

    private Exception? RunAndTryCatch(UnitTest test)
    {
        var method = test.Method;
        object[] parameters = test.Args ?? method.GetParameters()
            .Select(t => FormatterServices.GetUninitializedObject(t.ParameterType)).ToArray();
        var ex = test.Exception;

        try
        {
            object result;
            string message = test.ErrorMessage;
            var debugAssertFailed = message != null && message.Contains("Debug.Assert failed");
            result = method.Invoke(test.ThisArg, parameters);
        }
        catch (TargetInvocationException e)
        {
            var exceptionExpected = e.InnerException != null && e.InnerException.GetType() == ex;
            if (exceptionExpected)
            {
                return e.InnerException;
            }
            else if (e.InnerException != null && ex != null)
            {
                _logger.Warning($"Founded inner exception with unexpected type: {e.InnerException.GetType().FullName}");
                // maybe return e.InnerException?
            }
        }
        return null;
    }

    private Dictionary<Target, List<Exception>> TryProveTargets(List<Target> targets, HashSet<Exception> exceptions)
    {
        var result = new Dictionary<Target, List<Exception>>(); 
        foreach (var t in targets)
        {
            var loc = t.Result.Locations[0].PhysicalLocation;
            var file = loc.ArtifactLocation.Uri.AbsolutePath;

            var pattern1 = $"{file}:line {loc.Region.StartLine}";
            var pattern2 = $"{file}, {loc.Region.StartLine}";

            var proofs = new List<Exception>();
            foreach (var ex in exceptions)
            {
                if (!ExceptionHasNeededType(t, ex) || ex.StackTrace == null) {
                    continue;
                }
                if (ex.StackTrace.Contains(pattern1) || ex.StackTrace.Contains(pattern2)) {
                    proofs.Add(ex);
                }
            }
            if (proofs.Count > 0) {
                result[t] = proofs;
            }
        }
        return result;
    }

    private bool ExceptionHasNeededType(Target target, Exception ex)
    {
        if (target.Issue == hypothesisType.NullDereference)
        {
            return ex.GetType() == typeof(NullReferenceException);
        }

        if (target.Issue == hypothesisType.IndexOutOfRange)
        {
            return ex.GetType() == typeof(IndexOutOfRangeException);
        }

        return false;
    }

    public static IEnumerable<object[]> AnalyzedProjects()
    {
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/LoanExam/bin/Debug/net6.0/linux-x64/publish"
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/loan_exam.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/rd/rd-net/Test.Lifetimes/bin/Debug/net35/linux-x64/publish",
        //         "../../../../../benchmark/projects/rd/rd-net/Lifetimes/bin/Debug/net35/linux-x64/publish",
        //         "../../../../../benchmark/projects/rd/rd-net/Test.Reflection.App/bin/Debug/net472/linux-x64/publish",
        //         "../../../../../benchmark/projects/rd/rd-net/RdFramework/bin/Debug/net35/linux-x64/publish",
        //         "../../../../../benchmark/projects/rd/rd-net/Test.RdFramework/bin/Debug/net35/linux-x64/publish",
        //         "../../../../../benchmark/projects/rd/rd-net/RdFramework.Reflection/bin/Debug/net35/linux-x64/publish",
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/rd.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/litedb/LiteDB.Stress/bin/Debug/netcoreapp3.1/publish",
        //         "../../../../../benchmark/projects/litedb/LiteDB.Tests/bin/Debug/netcoreapp3.1/publish",
        //         "../../../../../benchmark/projects/litedb/LiteDB.Benchmarks/bin/Debug/netcoreapp3.1/publish",
        //         "../../../../../benchmark/projects/litedb/LiteDB/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/litedb/LiteDB.Shell/bin/Debug/netcoreapp3.1/publish"
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/litedb_LiteDB.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/NLog/src/NLog.Database/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/NLog/src/NLog/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/NLog/src/NLog.WindowsEventLog/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/NLog/src/NLog.OutputDebugString/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/NLog/src/NLog.WindowsRegistry/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/NLog/tests/NLog.WindowsRegistry.Tests/bin/Debug/netcoreapp3.1/publish",
        //         "../../../../../benchmark/projects/NLog/tests/NLogAutoLoadExtension/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/NLog/tests/SampleExtensions/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/NLog/tests/NLog.UnitTests/bin/Debug/netcoreapp3.1/publish",
        //         "../../../../../benchmark/projects/NLog/tests/NLog.Database.Tests/bin/Debug/netcoreapp3.1/publish",
        //         "../../../../../benchmark/projects/NLog/tests/ManuallyLoadedExtension/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/NLog/tests/PackageLoaderTestAssembly/bin/Debug/netstandard2.0/publish",
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/NLog_src_NLog.sarif"
        // };
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
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/moq4/src/Moq/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/moq4/tests/Moq.Tests.FSharpTypes/bin/Debug/netcoreapp3.1/publish",
        //         "../../../../../benchmark/projects/moq4/tests/Moq.Tests.FSharpTypes/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/moq4/tests/Moq.Tests.VisualBasic/bin/Debug/netcoreapp3.1/publish",
        //         "../../../../../benchmark/projects/moq4/tests/Moq.Tests.VisualBasic/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/moq4/tests/Moq.Tests/bin/Debug/netcoreapp3.1/publish",
        //         "../../../../../benchmark/projects/moq4/tests/Moq.Tests/bin/Debug/net6.0/publish",
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/moq4_Moq.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/nunit/bin/Debug/net6.0",
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/nunit_nunit.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.tdnet.tests/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.inproc.console/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.inproc.console.tests/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.core/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.common/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.utility/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.core.tests/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.common.tests/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.tdnet/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.common/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.assert.tests/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.assert/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.common.tests/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/xunit/src/xunit.v3.runner.utility.tests/bin/Debug/net6.0/publish"
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/xunit.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/AutoMapper/src/IntegrationTests/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/AutoMapper/src/AutoMapper/bin/Debug/netstandard2.1/publish",
        //         "../../../../../benchmark/projects/AutoMapper/src/UnitTests/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/AutoMapper/src/Benchmark/bin/Debug/net6.0/publish",
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/AutoMapper_AutoMapper.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/spbu-homeworks-1/Semester2/Homework8/BTree.Tests/bin/Debug/net5.0/publish",
        //         "../../../../../benchmark/projects/spbu-homeworks-1/Semester2/Homework8/BTree/bin/Debug/net5.0/publish"
        //
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/spbu-homeworks-1_Semester2_Homework8_BTree_BTree.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/ILSpy/ICSharpCode.ILSpyCmd/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/ILSpy/ICSharpCode.Decompiler/bin/Debug/netstandard2.0/publish",
        //         "../../../../../benchmark/projects/ILSpy/ICSharpCode.ILSpyX/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/ILSpy/ICSharpCode.Decompiler.TestRunner/bin/Debug/net6.0-windows/publish",
        //         "../../../../../benchmark/projects/ILSpy/ICSharpCode.Decompiler.PowerShell/bin/Debug/netstandard2.0/publish"
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/ILSpy.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/OpenRA/bin/publish"
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/OpenRA.sarif"
        // };
        // yield return new object[]
        // {
        //     new[]
        //     {
        //         "../../../../../benchmark/projects/Newtonsoft.Json/Src/Newtonsoft.Json/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/Newtonsoft.Json/Src/Newtonsoft.Json.Tests/bin/Debug/net6.0/publish",
        //         "../../../../../benchmark/projects/Newtonsoft.Json/Src/Newtonsoft.Json.TestConsole/bin/Debug/net6.0/publish"
        //     },
        //     "../../../../../benchmark/tools/reports/pvs/Newtonsoft.Json_Src_Newtonsoft.Json.sarif"
        // };
    }
}
