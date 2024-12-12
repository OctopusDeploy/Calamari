using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.Commands.Executors
{
    public class GlobberGrouping
    {
        readonly ICalamariFileSystem fileSystem;
        const string GroupedDirectoryName = "grouped";
        public GlobberGrouping(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        /// <summary>
        /// Groups together files from glob patterns into directories.
        /// </summary>
        /// <param name="deployment"></param>
        /// <param name="rootDirectory"></param>
        /// <param name="globs"></param>
        /// <returns>Result of directories in the same order as globs parameter</returns>
        public GlobDirectory[] Group(string rootDirectory, List<string> globs)
        {
            if (globs.IsNullOrEmpty())
                return Array.Empty<GlobDirectory>();

            var packageDirectory =
                Path.Combine(rootDirectory, KubernetesDeploymentCommandBase.PackageDirectoryName) +
                Path.DirectorySeparatorChar;
            
            var directories = new List<GlobDirectory>();
            for (var i = 1; i <= globs.Count; i ++)
            {
                var glob = globs[i-1]; // We want 1-indexed paths
                var directoryPath = Path.Combine(rootDirectory, GroupedDirectoryName, i.ToString());
                var directory = new GlobDirectory( glob, directoryPath);
                fileSystem.CreateDirectory(directoryPath);

                var results = fileSystem.EnumerateFilesWithGlob(packageDirectory, glob);
                foreach (var file in results)
                {
                    var relativeFilePath = fileSystem.GetRelativePath(packageDirectory, file);
                    var targetPath = Path.Combine(directoryPath, relativeFilePath);
                    var targetDirectory = Path.GetDirectoryName(targetPath);
                    if (targetDirectory != null)
                    {
                        fileSystem.CreateDirectory(targetDirectory);
                    }
                    fileSystem.CopyFile(file, targetPath);
                }

                directories.Add(directory);
            }
            
            return directories.ToArray();
        }
    }
    
    public class GlobDirectory
    {
        public GlobDirectory(string glob, string directory)
        {
            Glob = glob;
            Directory = directory;
        }
        public string Glob { get; }
        public string Directory { get; }
    }
}