#nullable enable
using System;
using System.Text;
using Calamari.ArgoCD.Git;
using LibGit2Sharp;
using Blob = LibGit2Sharp.Blob;
using Commit = LibGit2Sharp.Commit;
using Repository = LibGit2Sharp.Repository;
using Signature = LibGit2Sharp.Signature;

namespace Calamari.Tests.ArgoCD.Git
{
    public static class RepositoryExtensionMethods
    {
        public static string ReadFileFromBranch(this Repository repo, GitBranchName branchName, string filename)
        {
            var fileTreeEntry = repo.Branches[branchName.Value].Tip[filename];
            
            var fileBlob = (Blob)fileTreeEntry.Target;  
            return fileBlob.GetContentText();
        }
        
        public static void AddFilesToBranch(this Repository repo, GitBranchName branchName, params (string Name, string? Content)[] files)
        {
            var signature = new Signature("Arbitrary Coder", "arbitrary@octopus.com", DateTimeOffset.Now);
            var message = "Commit: Code";

            var parentCommit = repo.Branches[branchName.Value].Tip;
            repo.Commit(parentCommit,
                        branchName.Value,
                        message,
                        signature,
                        files);
        }

        public static Commit Commit(this Repository repository,
                                    Commit? parent,
                                    string? branchName,
                                    string message,
                                    Signature? signature,
                                    params (string Name, string? Content)[] files)
        {
            // Commits for uninitialised repositories will have no parent, and will need to start with an empty tree.
            var treeDefinition = parent is null ? new TreeDefinition() : TreeDefinition.From(parent.Tree);

            foreach (var file in files)
            {
                if (file.Content is null)
                {
                    treeDefinition.Remove(file.Name);
                }
                else
                {
                    var bytes = Encoding.UTF8.GetBytes(file.Content);
                    var blobId = repository.ObjectDatabase.Write<Blob>(bytes);
                    treeDefinition.Add(file.Name, blobId, Mode.NonExecutableFile);
                }
            }

            return repository.CommitTreeDefinition(parent,
                                                   branchName,
                                                   message,
                                                   signature,
                                                   treeDefinition);
        }

        static Commit CommitTreeDefinition(this Repository repository,
                                           Commit? parent,
                                           string? branchName,
                                           string message,
                                           Signature? signature,
                                           TreeDefinition treeDefinition)
        {
            // Write the tree to the object database
            var tree = repository.ObjectDatabase.CreateTree(treeDefinition);

            // Create the commitbareOrigin
            var parents = parent is null ? Array.Empty<Commit>() : new[] { parent };
            var commit = repository.ObjectDatabase.CreateCommit(
                                                                signature,
                                                                signature,
                                                                message,
                                                                tree,
                                                                parents,
                                                                false);

            if (branchName != null)
            {
                // Point the branch at the new commit if a branch name
                // has been provided
                var branch = repository.Branches[branchName];

                if (branch is null)
                {
                    repository.Branches.Add(branchName, commit);
                }
                else
                {
                    repository.Refs.UpdateTarget(branch.Reference, commit.Id);
                }
            }

            return commit;
        }
    }
}
