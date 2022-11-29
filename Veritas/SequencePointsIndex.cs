using Microsoft.CodeAnalysis.Sarif;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Veritas;

public class SequencePointsIndex : ISequencePointsIndex
{
    private readonly Dictionary<string, HashSet<MarkedInstruction>> _index = new();
    private readonly ReaderParameters _readerParameters = new() { ReadSymbols = true };

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
            catch (Exception ex)
            {
                // TODO: need to add logging
                Console.WriteLine(ex);
            }
        }
    }

    private void IndexAssembly(string assemblyPath)
    {
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, _readerParameters);

        var methods = assemblyDefinition.MainModule
            .GetTypes()
            .SelectMany(t => t.Methods.Where(m => m.HasBody));

        var points = methods
            .Where(m => m.DebugInformation.HasSequencePoints)
            .SelectMany(m => m.DebugInformation.SequencePoints)
            .Where(p => !p.IsHidden)
            .Select(p => new MarkedInstruction(p));

        var newIndex = points.GroupBy(p => p.DocumentPath).ToDictionary(g => g.Key, g => g.ToHashSet());
        MergeIndex(newIndex);
    }

    public List<MarkedInstruction> FindInstructions(PhysicalLocation location)
    {
        var sourceFilePath = location.ArtifactLocation.Uri.AbsolutePath;
        if (!_index.ContainsKey(sourceFilePath))
        {
            return new List<MarkedInstruction>();
        }
        
        var startLine = location.Region.StartLine;
        var sf = _index[sourceFilePath];
        return sf.Where(sp => sp.StartLine == startLine).ToList();
    }

    private void MergeIndex(Dictionary<string, HashSet<MarkedInstruction>> newIndex)
    {
        foreach (var (url, points) in newIndex)
        {
            if (_index.ContainsKey(url))
            {
                _index[url].UnionWith(points);
                continue;
            }

            _index[url] = points;
        }
    }
}