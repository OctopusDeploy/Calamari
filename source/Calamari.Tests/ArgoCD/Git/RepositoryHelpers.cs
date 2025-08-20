using System;
using System.IO;
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
        public static void CreateBranchIn(string branchName, string originPath)
        {
            var signature = new Signature("Your Name", "your.email@example.com", DateTimeOffset.Now);
            
            var repository = new Repository(originPath);
            repository.Refs.UpdateTarget("HEAD", "refs/heads/master");
            var tree = repository.ObjectDatabase.CreateTree(new TreeDefinition());
            var commit = repository.ObjectDatabase.CreateCommit(
                                                                signature,
                                                                signature,
                                                                "InitializeRepo",
                                                                tree,
                                                                Array.Empty<Commit>(),
                                                                false);
            repository.CreateBranch(branchName, commit);
        }
    }
}