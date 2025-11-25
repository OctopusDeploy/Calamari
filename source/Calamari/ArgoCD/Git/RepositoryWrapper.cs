#if NET
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using LibGit2Sharp;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Git
{
    public class RepositoryWrapper : IDisposable
    {
        readonly IRepository repository;
        readonly ICalamariFileSystem calamariFileSystem;
        readonly string repoCheckoutDirectoryPath;
        readonly ILog log;
        readonly IGitConnection connection;
        readonly IGitVendorApiAdapter? vendorApiAdapter;

        public string WorkingDirectory => repository.Info.WorkingDirectory;

        public RepositoryWrapper(IRepository repository,
                                 ICalamariFileSystem calamariFileSystem,
                                 string repoCheckoutDirectoryPath,
                                 ILog log,
                                 IGitConnection connection,
                                 IGitVendorApiAdapter? vendorApiAdapter)
        {
            this.repository = repository;
            this.calamariFileSystem = calamariFileSystem;
            this.repoCheckoutDirectoryPath = repoCheckoutDirectoryPath;
            this.log = log;
            this.connection = connection;
            this.vendorApiAdapter = vendorApiAdapter;
        }

        // returns true if changes were made to the repository
        public bool CommitChanges(string summary, string description)
        {
            try
            {
                var commitTime = DateTimeOffset.Now;
                var commitMessage = GenerateCommitMessage(summary, description);
                var commit = repository.Commit(commitMessage,
                                               new Signature("Octopus", "octopus@octopus.com", commitTime),
                                               new Signature("Octopus", "octopus@octopus.com", commitTime));
                log.Verbose($"Committed changes to {commit.ShortSha()}");
                return true;
            }
            catch (EmptyCommitException)
            {
                log.Verbose("No changes required committing.");
                return false;
            }
        }

        public void RecursivelyStageFilesForRemoval(string subPath)
        {
            var cleansedSubPath = subPath.StartsWith("./") ? subPath.Substring(2) : subPath;
            if (!cleansedSubPath.EndsWith("/") && cleansedSubPath.IsNullOrEmpty())
            {
                cleansedSubPath += "/";
            }

            log.Info("Removing files recursively");
            List<IndexEntry> filesToRemove = repository.Index.Where(i => i.Path.StartsWith(cleansedSubPath)).ToList();
            filesToRemove.ForEach(i => repository.Index.Remove(i.Path));
        }

        public void StageFiles(string[] filesToStage)
        {
            foreach (var file in filesToStage)
            {
                var fileToAdd = file.StartsWith("./") ? file.Substring(2) : file;
                repository.Index.Add(fileToAdd);
            }
        }

        public async Task PushChanges(bool requiresPullRequest,
                                      string summary,
                                      string description,
                                      GitReference branchName,
                                      CancellationToken cancellationToken)
        {
            var currentBranchName = repository.GetBranchName(branchName);
            var commit = repository.Head.Tip; // We should have just pushed to the tip of this branch

            var pushToBranchName = requiresPullRequest ? 
                CalculateBranchName() :
                currentBranchName;

            log.Info($"Pushing changes to branch '{pushToBranchName.ToFriendlyName()}'");
            PushChanges(pushToBranchName);

            if (vendorApiAdapter != null)
            {
                
                var url = vendorApiAdapter.GenerateCommitUrl(commit.Sha);
                log.Info($"Commit {log.FormatLink(url, commit.ShortSha())} pushed");    
            }
            else
            {
                log.Info($"Commit {commit.ShortSha()} pushed");    
            }
            
            if (requiresPullRequest)
            {
                await CreatePullRequest(summary, description, cancellationToken, pushToBranchName, currentBranchName);
            }
        }

        async Task CreatePullRequest(string summary,
                                     string description,
                                     CancellationToken cancellationToken,
                                     GitBranchName pushToBranchName,
                                     GitBranchName currentBranchName)
        {
            
            
            if (vendorApiAdapter == null)
            {
                throw new CommandException("No Git provider can be resolved based on the provided repository details");
            }
            
            try
            {
                log.Verbose($"Attempting to create pull request to {connection.Url}");
                var pullRequest = await vendorApiAdapter.CreatePullRequest(summary,
                                                                           description,
                                                                           pushToBranchName,
                                                                           currentBranchName,
                                                                           cancellationToken);
                
                log.SetOutputVariableButDoNotAddToVariables("PullRequest.Title", pullRequest.Title);
                log.SetOutputVariableButDoNotAddToVariables("PullRequest.Number", pullRequest.Number.ToString());
                log.SetOutputVariableButDoNotAddToVariables("PullRequest.Url", pullRequest.Url);

                log.Info($"Pull Request [{pullRequest.Title} (#{pullRequest.Number})]({pullRequest.Url}) Created");
            }
            catch (Exception e)
            {
                throw new CommandException("Pull Request Creation Failed", e);
            }
        }

        GitBranchName CalculateBranchName()
        {
            return GitBranchName.CreateFromFriendlyName($"octopus-argo-cd-{Guid.NewGuid().ToString("N").Substring(0, 10)}");
        }

        public void PushChanges(GitBranchName branchName)
        {
            var remote = repository.Network.Remotes.Single();
            repository.Branches.Update(repository.Head,
                                       branch => branch.Remote = remote.Name,
                                       branch => branch.UpstreamBranch = branchName.Value);

            PushStatusError? errorsDetected = null;
            var pushOptions = new PushOptions
            {
                CredentialsProvider = (url, usernameFromUrl, types) =>
                                          new UsernamePasswordCredentials { Username = connection.Username, Password = connection.Password },
                OnPushStatusError = errors => errorsDetected = errors
            };

            repository.Network.Push(repository.Head, pushOptions);
            if (errorsDetected != null)
            {
                throw new CommandException($"Failed to push to branch {branchName.ToFriendlyName()} - {errorsDetected.Message}");
            }
        }

        static string GenerateCommitMessage(string summary, string description)
        {
            return description.Equals(string.Empty)
                ? summary
                : $"{summary}\n\n{description}";
        }

        public void Dispose()
        {
            //free up the repository handles
            repository?.Dispose();

            //delete the local repository
            log.Verbose("Deleting local repository");
            try
            {
                //some files in the .git folder can/are ReadOnly which makes them impossible to delete
                //so just remove the ReadOnly attribute from all files (if they are ReadOnly)
                foreach (var gitFile in calamariFileSystem.EnumerateFilesRecursively(Path.Combine(repoCheckoutDirectoryPath, ".git")))
                {
                    calamariFileSystem.RemoveReadOnlyAttributeFromFile(gitFile);
                }

                calamariFileSystem.DeleteDirectory(repoCheckoutDirectoryPath);
                log.Verbose("Deleted local repository");
            }
            catch (Exception e)
            {
                log.VerboseFormat("Failed to delete local repository.{0}{1}", Environment.NewLine, e);
            }
        }
    }
}
#endif