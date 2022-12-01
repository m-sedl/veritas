using Microsoft.CodeAnalysis.Sarif;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Veritas;

public class SequencePointsIndex : ISequencePointsIndex
{
    private readonly Dictionary<string, HashSet<PointInfo>> _index = new();

    private readonly ReaderParameters _readerParameters = new() { ReadSymbols = true };

    private readonly MethodSearcher _methodSearcher;

    public SequencePointsIndex(List<string> assemblyPaths)
    {
        _methodSearcher = new MethodSearcher(assemblyPaths);
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
        var methods = assemblyDefinition.Modules
            .SelectMany(m => m.GetTypes())
            .SelectMany(t => t.Methods.Where(m => m.HasBody));

        IndexMethods(methods);
    }

    private void IndexMethods(IEnumerable<MethodDefinition> methods)
    {
        foreach (var m in methods.Where(m => m.DebugInformation.HasSequencePoints))
        {
            var ps = m.DebugInformation.SequencePoints;
            foreach (var p in ps.Where(p => !p.IsHidden))
            {
                if (!_index.ContainsKey(p.Document.Url))
                {
                    _index[p.Document.Url] = new HashSet<PointInfo>();
                }

                var vsMethod = _methodSearcher.FindMethod(m.MetadataToken.ToInt32());
                _index[p.Document.Url].Add(new PointInfo(p, vsMethod));
            }
        }
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