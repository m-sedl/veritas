using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Veritas.Tests;

static class Utils {
    static public IEnumerable<string> GetAllDlls(string path)
    {
        var dir = new DirectoryInfo(path);
        return dir
            .GetFiles("*.dll", SearchOption.AllDirectories)
            .Select(f => f.FullName);
    }
}
