using System;
using System.IO;
using System.Reflection;

namespace Calamari.Integration.Processes
{
    public static class AssemblyExtensions
    {
        public static string FullLocalPath(this Assembly assembly)
        {
            var codeBase = assembly.CodeBase;
            var uri = new UriBuilder(codeBase);
            var root = Uri.UnescapeDataString(uri.Path);
            root = root.Replace('/', Path.DirectorySeparatorChar);
            return root;
        }
    }
}
