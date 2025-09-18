#if NET
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
                if (fileSystem.FileExists(Path.Combine(rootPath, fileName)))
                {
                    return fileName;
                }
            }

            return null;
        }
    }
}
#endif