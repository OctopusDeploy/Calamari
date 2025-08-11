using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Commands.Executors;
using Calamari.Common.Commands;
using Calamari.Deployment.Conventions;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Conventions
{
    public class GitPushConvention : IInstallConvention
    {
        readonly List<Repository> repositories;

        public GitPushConvention(List<Repository> repositories)
        {
            this.repositories = repositories;
        }

        public void Install(RunningDeployment deployment)
        {
            
        }
        
        void ApplyChangesToLocalRepository(IEnumerable<FileToCopy> filesToCopy, Repository repository, string repoSubFolder)
        {
            foreach (var file in filesToCopy)
            {
                var repoRelativeFilePath = Path.Combine(repoSubFolder, file.RelativePath);
                var absRepoFilePath = Path.Combine(repository.Info.WorkingDirectory, repoRelativeFilePath);
                EnsureParentDirectoryExists(absRepoFilePath);
                File.Copy(file.AbsolutePath, absRepoFilePath, true);
                
                //This MUST take a path relative to the repository root.
                repository.Index.Add(repoRelativeFilePath);
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