#if NET
using System;
using System.IO;
using Calamari.ArgoCD.Git;
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

        public const string MainBranchName = "main";
        
        public static void CreateBranchIn(GitBranchName branchName, string originPath)
        {
            var cannonicalMainBranch = $"refs/heads/{MainBranchName}";
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

            repository.CreateBranch(MainBranchName, emptyCommit);

            repository.Refs.UpdateTarget("HEAD", cannonicalMainBranch);

            //create our branch
            repository.CreateBranch(branchName.Value, emptyCommit);
        }
    }
}
#endif