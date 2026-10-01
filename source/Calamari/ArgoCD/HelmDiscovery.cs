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
        
        public static string? TryFindValuesFile(ICalamariFileSystem fileSystem, string rootPath)
        {
            string[] filenames = {"values", "Values"};

            foreach (var filename in filenames)
            {
                foreach (var ext in ArgoCDConstants.SupportedAppFileExtensions)
                {
                    var fullFilename = $"{filename}{ext}";
                    var path = Path.Combine(rootPath, fullFilename);
                    if (fileSystem.FileExists(path))
                    {
                        return fullFilename; // JUST the basename, no path.
                    }
                }
            }

            return null;

        }
    }
}
