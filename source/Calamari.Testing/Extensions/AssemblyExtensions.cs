using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Calamari.Testing.Extensions;

public static class AssemblyExtensions
{
    public static Stream GetManifestResourceStreamFromPartialName(this Assembly assembly, string filename)
    {        
        var valuesFileName = assembly.GetManifestResourceNames().Single(n => n.Contains(filename));
        return assembly.GetManifestResourceStream(valuesFileName)!;
    }
}