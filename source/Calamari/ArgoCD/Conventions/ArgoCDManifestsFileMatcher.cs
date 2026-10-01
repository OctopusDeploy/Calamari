using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.ArgoCD.Conventions
{
    public interface IArgoCDManifestsFileMatcher : ISubstituteFileMatcher
    {
        IPackageRelativeFile[] FindMatchingPackageFiles(string currentDirectory, string target);
    }
    
    public class ArgoCDManifestsFileMatcher : IArgoCDManifestsFileMatcher
    {
        readonly ICalamariFileSystem fileSystem;
        
        public ArgoCDManifestsFileMatcher(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }
        public List<string> FindMatchingFiles(string currentDirectory, string target)
        {
            return FindMatchingPackageFiles(currentDirectory, target).Select(f => f.AbsolutePath).ToList();
        }

        public IPackageRelativeFile[] FindMatchingPackageFiles(string currentDirectory, string target)
        {
            var absoluteTargetPath = Path.Combine(currentDirectory,target);
            if (File.Exists(absoluteTargetPath))
            {
                return new IPackageRelativeFile[]{ new PackageRelativeFile(absolutePath: absoluteTargetPath, packageRelativePath: Path.GetFileName(absoluteTargetPath)) };
            }

            if (Directory.Exists(absoluteTargetPath))
            {
                return fileSystem.EnumerateFilesRecursively(absoluteTargetPath, "*").Select(absoluteFilepath =>
                                                                                            {
                                                                                                var relativePath = Path.GetRelativePath(absoluteTargetPath, absoluteFilepath);
                                                                                                return new PackageRelativeFile(absoluteFilepath, relativePath);
                                                                                            })
                                 .ToArray<IPackageRelativeFile>();
            }

            return Array.Empty<IPackageRelativeFile>();
        }
    }
}
