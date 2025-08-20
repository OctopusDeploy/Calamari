using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions
{
    public class FileWriter
    {
        readonly ICalamariFileSystem fileSystem;
        readonly PackageRelativeFile[] packageFiles;

        public FileWriter(ICalamariFileSystem fileSystem, PackageRelativeFile[] packageFiles)
        {
            this.fileSystem = fileSystem;
            this.packageFiles = packageFiles;
        }

        public IReadOnlyList<string> ApplyFilesTo(string destinationRootDir, string subFolder)
        {
            var rootDirRelativeFiles = new List<string>();
            foreach (var file in packageFiles)
            {
                var rootDirRelativeFilePath = Path.Combine(subFolder, file.PackageRelativePath);
                var absDestinationPath = Path.Combine(destinationRootDir, rootDirRelativeFilePath);
                Log.VerboseFormat($"Copying '{file.AbsolutePath}' to '{absDestinationPath}'");
                EnsureParentDirectoryExists(absDestinationPath);
                fileSystem.CopyFile(file.AbsolutePath, absDestinationPath);
                
                rootDirRelativeFiles.Add(rootDirRelativeFilePath); //This MUST take a path relative to the repository root.
            }

            return rootDirRelativeFiles.AsReadOnly();
        }

        static void EnsureParentDirectoryExists(string filePath)
        {
            var destinationDirectory = Path.GetDirectoryName(filePath);
            if (destinationDirectory != null)
            {
                Directory.CreateDirectory(destinationDirectory);    
            }
        }
    }
}