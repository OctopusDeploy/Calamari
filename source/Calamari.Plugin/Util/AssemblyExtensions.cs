using System.Diagnostics;
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
    }
}