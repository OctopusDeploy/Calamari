using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Time;
using LibGit2Sharp;
using Polly;
using Polly.Retry;

namespace Calamari.ArgoCD.Git
{
    public class RepositoryWrapper(
        Repository repository,
        ICalamariFileSystem calamariFileSystem,
        string repoCheckoutDirectoryPath,
        ILog log,
        IGitConnection connection,
        IGitVendorPullRequestClient? vendorApiAdapter,
        IClock clock)
        : IDisposable
    {
        // ReSharper disable ReplaceWithPrimaryConstructorParameter - This makes parameters readonly
        readonly Repository repository = repository;
        readonly ICalamariFileSystem calamariFileSystem = calamariFileSystem;
        readonly string repoCheckoutDirectoryPath = repoCheckoutDirectoryPath;
        readonly ILog log = log;
        readonly IGitConnection connection = connection;
        readonly IGitVendorPullRequestClient? vendorApiAdapter = vendorApiAdapter;
        readonly IClock clock = clock;
        // ReSharper restore ReplaceWithPrimaryConstructorParameter

        readonly Identity repositoryIdentity = new("Octopus", "octopus@octopus.com");

        public string WorkingDirectory => repository.Info.WorkingDirectory;

        Credentials RepositoryCredentials => connection switch
             {
                 SshGitConnection ssh => new SshUserKeyMemoryCredentials { Username = ssh.Username, PublicKey = ssh.PublicKey, PrivateKey = ssh.PrivateKey, Passphrase = ssh.Passphrase },
                 HttpsGitConnection https => new UsernamePasswordCredentials { Username = https.Username, Password = https.Password },
                 _ => null
             };

        // returns true if changes were made to the repository
        public bool CommitChanges(string summary, string description)
        {
            try
            {
                var commitTime = clock.GetUtcTime();
                var commitMessage = GenerateCommitMessage(summary, description);
                var commit = repository.Commit(commitMessage,
                                               new Signature(repositoryIdentity, commitTime),
                                               new Signature(repositoryIdentity, commitTime));
                log.Verbose($"Committed changes to {commit.ShortSha()}");
                return true;
            }
            catch (EmptyCommitException)
            {
                log.Verbose("No changes required committing.");
                return false;
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to commit changes to git repository. Error: {e.Message}", e);
            }
        }

        public void StageAllChanges()
        {
            try
            {
                LibGit2Sharp.Commands.Stage(repository, "*");
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to stage files in git repository. Error: {e.Message}", e);
            }
        }

        public async Task<PushResult> PushChanges(bool requiresPullRequest,
                                                  string summary,
                                                  string description,
                                                  GitReference branchName,
                                                  CancellationToken cancellationToken)
        {
            var currentBranchName = repository.GetBranchName(branchName);
            var pushToBranchName = requiresPullRequest ? CalculateBranchName() : currentBranchName;

            log.Info($"Pushing changes to branch '{pushToBranchName.ToFriendlyName()}'");

            var retryPipeline = new ResiliencePipelineBuilder()
                                .AddRetry(new RetryStrategyOptions
                                {
                                    ShouldHandle = new PredicateBuilder().Handle<CommandException>().Handle<NonFastForwardException>(),
                                    MaxRetryAttempts = 2,
                                    UseJitter =  true,
                                    Delay = TimeSpan.FromSeconds(2),
                                    OnRetry = args =>
                                    {
                                        log.Verbose($"Push to '{pushToBranchName.ToFriendlyName()}' failed (attempt {args.AttemptNumber + 1}), fetching and rebasing before retrying");
                                        FetchAndRebase(currentBranchName);
                                        return default;
                                    }
                                })
                                .Build();

            try
            {
                retryPipeline.Execute(() => PushChanges(pushToBranchName));
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to push to branch '{pushToBranchName.ToFriendlyName()}'. Error: {e.Message}", e);
            }

            var commit = repository.Head.Tip;

            if (vendorApiAdapter != null)
            {
                var url = vendorApiAdapter.GenerateCommitUrl(commit.Sha);
                log.Info($"Commit {log.FormatLink(url, commit.ShortSha())} pushed");
            }
            else
            {
                log.Info($"Commit {commit.ShortSha()} pushed");
            }

            if (!requiresPullRequest)
            {
                return new PushResult(commit.Sha, commit.ShortSha(), commit.Author.When);
            }

            var (title, number, uri) = await CreatePullRequest(
                summary,
                description,
                pushToBranchName,
                currentBranchName,
                cancellationToken);

            return new PullRequestPushResult(
                commit.Sha,
                commit.ShortSha(),
                commit.Author.When,
                connection.Url,
                title,
                uri,
                number);
        }

        async Task<PullRequest> CreatePullRequest(
            string summary,
            string description,
            GitBranchName pushToBranchName,
            GitBranchName currentBranchName,
            CancellationToken cancellationToken)
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

                return pullRequest;
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Pull Request Creation Failed. Error: {e.Message}", e);
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
                CredentialsProvider = (url, usernameFromUrl, types) => RepositoryCredentials,
                OnPushStatusError = errors => errorsDetected = errors,
                CertificateCheck = connection is SshGitConnection ? SshHostKeyVerificationBypass.AcceptAll : null
            };

            repository.Network.Push(repository.Head, pushOptions);
            if (errorsDetected != null)
            {
                throw new CommandException($"Failed to push to branch {branchName.ToFriendlyName()}. Error: {errorsDetected.Message}");
            }
        }

        void FetchAndRebase(GitBranchName branchName)
        {
            var remote = repository.Network.Remotes.Single();
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification).ToList();
            var fetchOptions = new FetchOptions
            {
                CredentialsProvider = (url, usernameFromUrl, types) => RepositoryCredentials,
                CertificateCheck = connection is SshGitConnection ? SshHostKeyVerificationBypass.AcceptAll : null
            };

            try
            {
                log.Verbose($"Fetching from remote '{remote.Name}'");
                LibGit2Sharp.Commands.Fetch(repository, remote.Name, refSpecs, fetchOptions, null);
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to fetch from remote '{remote.Name}'. Error: {e.Message}", e);
            }

            var trackingBranchName = $"{remote.Name}/{branchName.ToFriendlyName()}";
            var trackingBranch = repository.Branches[trackingBranchName];
            if (trackingBranch == null)
            {
                log.Verbose($"No tracking branch found for '{branchName.ToFriendlyName()}', skipping rebase");
                return;
            }

            log.Verbose($"Rebasing onto '{trackingBranch.FriendlyName}'");
            try
            {
                var rebaseResult = repository.Rebase.Start(null,
                                                           trackingBranch,
                                                           null,
                                                           repositoryIdentity,
                                                           new RebaseOptions());
                if (rebaseResult.Status == RebaseStatus.Conflicts)
                {
                    throw new CommandException($"Rebase conflict detected when rebasing onto '{trackingBranch.FriendlyName}'. Error: Cannot automatically resolve conflicts");
                }

                log.Verbose($"Rebase result: {rebaseResult.Status}");
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to rebase onto '{trackingBranch.FriendlyName}'. Error: {e.Message}", e);
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

    public record PushResult(string CommitSha, string ShortSha, DateTimeOffset CommitTimestamp);

    public record PullRequestPushResult(
        string CommitSha,
        string ShortSha,
        DateTimeOffset CommitTimestamp,
        string RepositoryUri,
        string PullRequestTitle,
        string PullRequestUri,
        long PullRequestNumber) : PushResult(CommitSha, ShortSha, CommitTimestamp);
}
