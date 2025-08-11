using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Commands.Executors;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Conventions
{
    class FileToCopy
    {
        public FileToCopy(string absolutePath, string relativePath)
        {
            RelativePath = relativePath;
            AbsolutePath = absolutePath;
        }

        public string AbsolutePath { get; }
        public string RelativePath { get; }
    }

    public class UpdateRepositoryConvention : IInstallConvention
    {
        readonly List<Repository> repositories;
        readonly ICalamariFileSystem fileSystem;

        public UpdateRepositoryConvention(List<Repository> repositories, ICalamariFileSystem fileSystem)
        {
            this.repositories = repositories;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var folder = deployment.Variables.Get(SpecialVariables.Git.Folder);
            var fileBlobs = deployment.Variables.GetPaths(SpecialVariables.CustomResourceYamlFileName);

            var filesToInclude = SelectFiles(deployment.CurrentDirectory, fileBlobs);

            ApplyChangesToLocalRepository(filesToInclude, folder);

            throw new System.NotImplementedException();
        }

        IEnumerable<FileToCopy> SelectFiles(string pathToExtractedPackage, List<string> fileGlobs)
        {
            return fileGlobs.SelectMany(glob => fileSystem.EnumerateFilesWithGlob(pathToExtractedPackage, glob))
                            .Select(absoluteFilepath =>
                                    {
#if NETCORE
                                        var relativePath = Path.GetRelativePath(pathToExtractedPackage, file);
#else
                                        var relativePath = absoluteFilepath.Substring(pathToExtractedPackage.Length + 1);
#endif
                                        return new FileToCopy(absoluteFilepath, relativePath);
                                    });
        }

        void ApplyChangesToLocalRepository(IEnumerable<FileToCopy> filesToCopy, string repoSubFolder)
        {
            foreach (var file in filesToCopy)
            {
                var repoRelativeFilePath = Path.Combine(repoSubFolder, file.RelativePath);
                foreach(var repo in repositories)
                {
                    var absRepoFilePath = Path.Combine(repo.Info.WorkingDirectory, repoRelativeFilePath);
                    EnsureParentDirectoryExists(absRepoFilePath);
                    File.Copy(file.AbsolutePath, absRepoFilePath, true);
                    //This MUST take a path relative to the repository root.
                    repo.Index.Add(repoRelativeFilePath);
                }
            }
        }

        void EnsureParentDirectoryExists(string filePath)
        {
            var destinationDirectory = Path.GetDirectoryName(filePath);
            if (destinationDirectory != null)
            {
                Directory.CreateDirectory(destinationDirectory);
            }
        }
    }
}