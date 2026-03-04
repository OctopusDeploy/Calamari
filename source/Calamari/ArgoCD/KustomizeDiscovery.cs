#nullable enable
using System;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.ArgoCD
{
    public class KustomizeDiscovery
    {
        public static string? TryFindKustomizationFile(ICalamariFileSystem fileSystem, string rootPath)
        {
            foreach (var fileName in ArgoCDConstants.KustomizationFileNames)
            {
                var absPath = Path.Combine(rootPath, fileName);
                if (fileSystem.FileExists(absPath))
                {
                    return absPath;
                }
            }

            return null;
        }
    }
}
