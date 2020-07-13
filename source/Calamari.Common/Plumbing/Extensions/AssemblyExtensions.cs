using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Calamari
{
    public static class AssemblyExtensions
    {
        public static string GetInformationalVersion(this Assembly assembly)
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fileVersionInfo.ProductVersion;
        }

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