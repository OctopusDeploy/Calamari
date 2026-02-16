using System;
using System.IO;
using Calamari.ArgoCD.Git;
using Calamari.Common.Plumbing.FileSystem;
using LibGit2Sharp;

namespace Calamari.Tests.ArgoCD.Git
{
    public static class RepositoryHelpers
    {
        public static Repository CreateBareRepository(string repositoryPath)
        {
            Directory.CreateDirectory(repositoryPath);
            Repository.Init(repositoryPath, isBare: true);
            return new Repository(repositoryPath);
        }

        public static GitBranchName MainBranchName = GitBranchName.CreateFromFriendlyName("main");
        
        public static void CreateBranchIn(GitBranchName branchName, string originPath)
        {
            var signature = new Signature("Your Name", "your.email@example.com", DateTimeOffset.Now);

            var repository = new Repository(originPath);
            var tree = repository.ObjectDatabase.CreateTree(new TreeDefinition());

            //create an empty commit
            var emptyCommit = repository.ObjectDatabase.CreateCommit(signature,
                                                                     signature,
                                                                     "Init",
                                                                     tree,
                                                                     Array.Empty<Commit>(),
                                                                     false);

            repository.CreateBranch(MainBranchName.ToFriendlyName(), emptyCommit);

            repository.Refs.UpdateTarget("HEAD", MainBranchName.Value);

            //create our branch
            repository.CreateBranch(branchName.ToFriendlyName(), emptyCommit);
        }

        public static string CloneOrigin(string tempDirectory, string originPath, GitBranchName branchName)
        {
            var subPath = Guid.NewGuid().ToString();
            var resultPath = Path.Combine(tempDirectory, subPath);
            Repository.Clone(originPath, resultPath);
            var resultRepo = new Repository(resultPath);
            LibGit2Sharp.Commands.Checkout(resultRepo, branchName.ToFriendlyName());

            return resultPath;
        }
        
        public static void DeleteRepositoryDirectory(ICalamariFileSystem fileSystem, string path)
        {
            //Some files might be ReadOnly, clean up properly by removing the ReadOnly attribute
            foreach (var file in fileSystem.EnumerateFilesRecursively(path))
            {
                fileSystem.RemoveReadOnlyAttributeFromFile(file);
            }

            fileSystem.DeleteDirectory(path, FailureOptions.IgnoreFailure);
        }
    }
    
    
}
