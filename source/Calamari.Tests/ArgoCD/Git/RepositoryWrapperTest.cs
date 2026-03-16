using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.Time;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using LibGit2Sharp;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git
{
    [TestFixture]
    public class RepositoryWrapperTest
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();

        InMemoryLog log;
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        string repositoryPath = "repository";
        Repository bareOrigin;
        GitBranchName branchName = GitBranchName.CreateFromFriendlyName("devBranch");

        IGitConnection gitConnection;
        RepositoryWrapper repository;
        IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory = Substitute.For<IGitVendorAgnosticApiAdapterFactory>();
        IGitVendorApiAdapter gitVendorApiAdapter = Substitute.For<IGitVendorApiAdapter>();

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();

            tempDirectory = fileSystem.CreateTemporaryDirectory();

            bareOrigin = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(branchName, OriginPath);

            gitVendorApiAdapter.CreatePullRequest(Arg.Any<string>(),
                                                  Arg.Any<string>(),
                                                  Arg.Any<GitBranchName>(),
                                                  Arg.Any<GitBranchName>(),
                                                  Arg.Any<CancellationToken>())
                               .Returns(new PullRequest("title", 1, "url"));
            gitVendorAgnosticApiAdapterFactory.TryCreateGitVendorApiAdaptor(Arg.Any<IRepositoryConnection>()).Returns(gitVendorApiAdapter);
            
            var repositoryFactory = new RepositoryFactory(log, fileSystem, tempDirectory, gitVendorAgnosticApiAdapterFactory, new SystemClock());
            gitConnection = new GitConnection(null, null, new Uri(OriginPath), branchName);
            repository = repositoryFactory.CloneRepository(repositoryPath, gitConnection);
        }

        [TearDown]
        public void Cleanup()
        {
            RepositoryHelpers.DeleteRepositoryDirectory(fileSystem, tempDirectory);
        }

        string RepositoryRootPath => Path.Combine(tempDirectory, repositoryPath);

        [Test]
        public void StagingANonExistentFileThrowsException()
        {
            Action act = () => repository.AddFiles(new[] { "nonexistent.txt" });
            act.Should().Throw<LibGit2SharpException>().And.Message.Should().Contain("could not find ");
        }

        [Test]
        public void EmptyCommitReturnsFalse()
        {
            var result = repository.CommitChanges("Summary Message", "There is no data to commit");
            result.Should().BeFalse();
        }

        [Test]
        public void AttemptingToAddFilestartingWithDotSlashSucceeds()
        {
            //This is to highlight a behaviour of libGit2Sharp which we may run into
            string filename = "newFile.txt";
            File.WriteAllText(Path.Combine(RepositoryRootPath, filename), "");
            repository.AddFiles(new[] { $"./{filename}" });
        }

        [Test]
        public async Task StagingARealFileSucceedsAndCanBeCommittedAndPushed()
        {
            string filename = "newFile.txt";
            string fileContents = "Lorem ipsum dolor sit amet";
            File.WriteAllText(Path.Combine(RepositoryRootPath, filename), fileContents);
            repository.AddFiles(new[] { filename });
            repository.CommitChanges("Summary Message", "A file has changed").Should().BeTrue();
            await repository.PushChanges(false,
                                         "Summary Message",
                                         "A file has changed",
                                         branchName,
                                         CancellationToken.None);

            //ensure the remote contains the file
            var originFileContent = bareOrigin.ReadFileFromBranch(branchName, filename);
            originFileContent.Should().Be(fileContents);
        }

        [Test]
        public async Task CanPushTheHeadToAnyBranchNameOnRemote()
        {
            string filename = "newFile.txt";
            File.WriteAllText(Path.Combine(RepositoryRootPath, filename), "");
            repository.AddFiles(new[] { filename });
            repository.CommitChanges("Summary Message", "There is no data to comm it").Should().BeTrue();
            await repository.PushChanges(false,
                                         "Summary Message",
                                         "There is no data to comm it",
                                         GitBranchName.CreateFromFriendlyName("arbitraryBranch1"),
                                         CancellationToken.None);
            await repository.PushChanges(false,
                                         "Summary Message",
                                         "There is no data to comm it",
                                         GitBranchName.CreateFromFriendlyName("arbitraryBranch2"),
                                         CancellationToken.None);
        }

        [Test]
        public async Task WhenCreatingAPrThePrTitleAndBodyMatchTheCommitMessageFields()
        {
            string filename = "newFile.txt";
            await File.WriteAllTextAsync(Path.Combine(RepositoryRootPath, filename), "");
            repository.AddFiles(new[] { filename });
            var commitSummary = "Summary Message";
            var commitDescription = "A commit description";
            repository.CommitChanges(commitSummary, commitDescription).Should().BeTrue();
            var prBranch = GitBranchName.CreateFromFriendlyName("arbitraryBranch");
            await repository.PushChanges(true,
                                         commitSummary,
                                         commitDescription,
                                         prBranch,
                                         CancellationToken.None);
            await gitVendorApiAdapter.Received(1)
                                                 .CreatePullRequest(
                                                                    commitSummary,
                                                                    commitDescription,
                                                                    Arg.Any<GitBranchName>(),
                                                                    prBranch,
                                                                    Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task WhenDisposingOfARepository_TheCheckoutDirectoryIsRemoved()
        {
            //Arrange 
            const string filename = "newFile.txt";
            const string fileContents = "Lorem ipsum dolor sit amet";
            await File.WriteAllTextAsync(Path.Combine(RepositoryRootPath, filename), fileContents);
            
            repository.AddFiles(new[] { filename });
            repository.CommitChanges("Summary Message", "A file has changed").Should().BeTrue();
            
            // Act
            repository.Dispose();
            
            // Assert
            fileSystem.DirectoryExists(RepositoryRootPath)
                      .Should()
                      .BeFalse();
        }

        [Test]
        public void CloningAReferenceOtherThanABranchFails()
        {
            bareOrigin.AddFilesToBranch(branchName, ("file.yaml", ""));
            bareOrigin.ApplyTag("1.0.0", bareOrigin.Head.Tip.Sha);

            gitConnection = new GitConnection(null, null, new Uri(OriginPath), GitReference.CreateFromString("1.0.0"));
            
            var repositoryFactory = new RepositoryFactory(log, fileSystem, tempDirectory, gitVendorAgnosticApiAdapterFactory, new SystemClock());
            var act = () => repositoryFactory.CloneRepository($"{repositoryPath}/sut", gitConnection);

            act.Should()
               .Throw<CommandException>()
               .WithMessage($"Failed to clone Git repository at {gitConnection.Url}. Are you sure this URL is a Git repository, and the reference is a branch?");
        }

        [Test]
        public async Task WhenRemoteHasNewCommitBeforePush_RetrySucceedsAfterFetchAndRebase()
        {
            // Arrange: commit a file in our clone
            const string filename = "ourFile.txt";
            const string fileContents = "our content";
            await File.WriteAllTextAsync(Path.Combine(RepositoryRootPath, filename), fileContents);
            repository.AddFiles([filename]);
            repository.CommitChanges("Our commit", "").Should().BeTrue();

            // Simulate a concurrent push to origin on a different file (causes non-fast-forward failure)
            bareOrigin.AddFilesToBranch(branchName, ("concurrentFile.txt", "concurrent content"));

            // Act: first push attempt will fail, retry should fetch+merge and succeed
            await repository.PushChanges(false, "Our commit", "", branchName, CancellationToken.None);

            // Assert: origin has our file
            bareOrigin.ReadFileFromBranch(branchName, filename).Should().Be(fileContents);

            // Assert: retry message was logged
            log.MessagesVerboseFormatted
               .Should()
               .Contain(m => m.Contains("fetching and rebasing before retrying"));
        }

        [Test]
        public async Task WhenRebaseConflictDuringRetry_ThrowsCommandException()
        {
            // Arrange: commit a change to a file in our clone
            const string conflictFile = "conflict.txt";
            File.WriteAllText(Path.Combine(RepositoryRootPath, conflictFile), "our content");
            repository.AddFiles(new[] { conflictFile });
            repository.CommitChanges("Our commit", "").Should().BeTrue();

            // Simulate a concurrent conflicting change to the same file in origin
            bareOrigin.AddFilesToBranch(branchName, (conflictFile, "their content"));

            // Act & Assert: push fails, FetchAndMerge detects conflict and throws
            Func<Task> act = () => repository.PushChanges(false, "Our commit", "", branchName, CancellationToken.None);
            await act.Should()
                     .ThrowAsync<CommandException>()
                     .WithMessage("*Rebase conflict*");
        }

        string CloneOrigin()
        {
            var subPath = Guid.NewGuid().ToString();
            var resultPath = Path.Combine(tempDirectory, subPath);
            Repository.Clone(OriginPath, resultPath);
            var resultRepo = new Repository(resultPath);
            LibGit2Sharp.Commands.Checkout(resultRepo, branchName.ToFriendlyName());

            return resultPath;
        }
    }
}
