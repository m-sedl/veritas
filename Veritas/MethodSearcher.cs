using System.Reflection;
using VSharp;

namespace Veritas;

public class MethodSearcher
{
    private readonly List<Assembly> _assemblies = new();

    private readonly Dictionary<int, Method> _methods = new();

    public MethodSearcher(List<string> assemblyPaths)
    {
        foreach (var path in assemblyPaths)
        {
            try
            {
                _assemblies.Add(AssemblyManager.Resolve(path));
            }
            catch (Exception)
            {
                // TODO: need to add logging
            }
        }
    }

    public Method FindMethod(int token)
    {
        if (_methods.ContainsKey(token))
        {
            return _methods[token];
        }

        foreach (var module in _assemblies.SelectMany(asm => asm.Modules))
        {
            try
            {
                var baseMethod = module.ResolveMethod(token);
                if (baseMethod is null) continue;

                var method = Application.getMethod(baseMethod);
                if (method is null)
                {
                    continue;
                }

                _methods[token] = method;
                return method;
            }
            catch (Exception)
            {
                // TODO: need to add logging
            }
        }

        throw new MethodNotFoundException(token);
    }
}