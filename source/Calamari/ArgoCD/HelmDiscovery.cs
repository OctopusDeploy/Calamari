#if NET
#nullable enable
using System.IO;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.ArgoCD
{
    public class HelmDiscovery
    {
        public static string? TryFindHelmChartFile(ICalamariFileSystem fileSystem, string rootPath)
        {
            var filename = Path.Combine(rootPath, ArgoCDConstants.HelmChartFileName);
            if (fileSystem.FileExists(filename))
            {
                return filename;
            }

            return null;
        }
    }
}
#endif