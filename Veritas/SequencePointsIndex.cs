using System.Reflection;
using Microsoft.CodeAnalysis.Sarif;
using Mono.Cecil;
using Mono.Cecil.Cil;
using VSharp;

namespace Veritas;

public class SequencePointsIndex : ISequencePointsIndex
{
    private readonly Dictionary<string, HashSet<PointInfo>> _index = new();

    private readonly ReaderParameters _readerParameters = new() { ReadSymbols = true };

    private readonly HashSet<string> _processedAssemblies = new();

    private readonly VeritasAssemblyLoadContext _alc = new("veritas_alc");

    public SequencePointsIndex(List<string> assemblyPaths)
    {
        foreach (var path in assemblyPaths)
        {
            try
            {
                IndexAssembly(path);
            }
            catch (SymbolsNotFoundException)
            {
                // Console.WriteLine($"Assembly {path} skipped because pdb not founded");
                // TODO: need to add logging
            }
            catch (BadImageFormatException)
            {
                // TODO: need to add logging
            }
        }
    }

    private void IndexAssembly(string assemblyPath)
    {
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, _readerParameters);
        if (_processedAssemblies.Contains(assemblyDefinition.FullName))
        {
            return;
        }

        Assembly assembly;
        try
        {
            assembly = _alc.LoadFromAssemblyPath(assemblyPath);
        }
        catch (BadImageFormatException)
        {
            //TODO add logging
            return;
        }

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
            foreach (var p in ps.Where(p => !p.IsHidden))
            {
                if (!_index.ContainsKey(p.Document.Url))
                {
                    _index[p.Document.Url] = new HashSet<PointInfo>();
                }
                _index[p.Document.Url].Add(new PointInfo(p, vsMethod));
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
        var sf = _index[sourceFilePath];
        return sf.Where(sp => sp.StartLine == startLine).ToList();
    }
}