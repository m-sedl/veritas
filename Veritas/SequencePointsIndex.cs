using System.Reflection;
using Microsoft.CodeAnalysis.Sarif;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Serilog;
using VSharp;

namespace Veritas;

public class SequencePointsIndex : ISequencePointsIndex
{
    private readonly Dictionary<string, HashSet<PointInfo>> _index = new();

    private readonly Dictionary<Method, List<PointInfo>> _reverseIndex = new();

    private readonly ReaderParameters _readerParameters = new() { ReadSymbols = true };

    private readonly HashSet<string> _processedAssemblies = new();

    private readonly ILogger _logger;

    public SequencePointsIndex(List<string> assemblyPaths, ILogger logger)
    {
        _logger = logger;
        _logger.Information($"Started indexing of sequence points for {assemblyPaths.Count} assemblies");
        foreach (var path in assemblyPaths)
        {
            try
            {
                IndexAssembly(path);
            }
            catch (SymbolsNotFoundException)
            {
                _logger.Debug($"Assembly {path} skipped because pdb not founded");
            }
            catch (BadImageFormatException ex)
            {
                _logger.Debug($"Exception during assembly ({path}) indexing: {ex.Message}");
            }
        }
        _logger.Information("Indexing completed");
    }

    private void IndexAssembly(string assemblyPath)
    {
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, _readerParameters);
        if (_processedAssemblies.Contains(assemblyDefinition.FullName))
        {
            return;
        }

        var assembly = AssemblyManager.LoadFromAssemblyPath(assemblyPath);
        var methods = assemblyDefinition.Modules
            .SelectMany(m => m.GetTypes())
            .SelectMany(t => t.Methods.Where(m => m.HasBody));

        IndexMethods(assembly, methods);
        _processedAssemblies.Add(assemblyDefinition.Name.FullName);
    }

    private void IndexMethods(Assembly assembly, IEnumerable<MethodDefinition> methods)
    {
        foreach (var m in methods.Where(m => m.DebugInformation.HasSequencePoints))
        {
            var ps = m.DebugInformation.SequencePoints;
            var token = m.MetadataToken.ToInt32();
            var vsMethod = FindMethod(assembly, token);

            if (!_reverseIndex.ContainsKey(vsMethod))
            {
                _reverseIndex[vsMethod] = new List<PointInfo>();
            }

            foreach (var p in ps.Where(p => !p.IsHidden))
            {
                if (!_index.ContainsKey(p.Document.Url))
                {
                    _index[p.Document.Url] = new HashSet<PointInfo>();
                }
                var point = new PointInfo(p, vsMethod);
                _index[p.Document.Url].Add(point);
                _reverseIndex[vsMethod].Add(point);
            }
        }
    }

    private Method FindMethod(Assembly assembly, int token)
    {
        foreach (var module in assembly.Modules)
        {
            var baseMethod = module.ResolveMethod(token);
            if (baseMethod is null) continue;

            var method = Application.getMethod(baseMethod);
            if (method is not null)
            {
                return method;
            }
        }
        throw new MethodNotFoundException(token);
    }

    public List<PointInfo> FindPoints(PhysicalLocation location)
    {
        var sourceFilePath = location.ArtifactLocation.Uri.AbsolutePath;
        if (!_index.ContainsKey(sourceFilePath))
        {
            return new List<PointInfo>();
        }

        var startLine = location.Region.StartLine;
        var endLine = location.Region.EndLine;
        var sf = _index[sourceFilePath];
        return sf.Where(sp => sp.StartLine >= startLine && endLine <= sp.EndLine).ToList();
    }

    public PointInfo? FindPoint(codeLocation location) 
    {
        var methodPoints = _reverseIndex.GetValueOrDefault(location.method);
        if (methodPoints == null) return null;
        return methodPoints.Last(p => p.Location.offset <= location.offset);
    }
}