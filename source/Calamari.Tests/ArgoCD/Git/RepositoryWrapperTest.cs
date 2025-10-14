#if NET
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Plumbing.FileSystem;
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
        GitBranchName branchName = new GitBranchName("devBranch");

        IGitHubPullRequestCreator gitHubPullRequestCreator = Substitute.For<IGitHubPullRequestCreator>();
        IGitConnection gitConnection;
        RepositoryWrapper repository;

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();

            tempDirectory = fileSystem.CreateTemporaryDirectory();

            bareOrigin = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(branchName, OriginPath);

            var repositoryFactory = new RepositoryFactory(log, fileSystem, tempDirectory, gitHubPullRequestCreator);
            gitConnection = new GitConnection(null, null, OriginPath, branchName);
            repository = repositoryFactory.CloneRepository(repositoryPath, gitConnection);
        }

        [TearDown]
        public void Cleanup()
        {
            //Some files might be ReadOnly, clean up properly by removing the ReadOnly attribute
            foreach (var file in fileSystem.EnumerateFilesRecursively(tempDirectory))
            {
                fileSystem.RemoveReadOnlyAttributeFromFile(file);
            }
            
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }

        string RepositoryRootPath => Path.Combine(tempDirectory, repositoryPath);

        [Test]
        public void StagingANonExistentFileThrowsException()
        {
            Action act = () => repository.StageFiles(new[] { "nonexistent.txt" });
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
            repository.StageFiles(new[] { $"./{filename}" });
        }

        [Test]
        public async Task StagingARealFileSucceedsAndCanBeCommittedAndPushed()
        {
            string filename = "newFile.txt";
            string fileContents = "Lorem ipsum dolor sit amet";
            File.WriteAllText(Path.Combine(RepositoryRootPath, filename), fileContents);
            repository.StageFiles(new[] { filename });
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
            repository.StageFiles(new[] { filename });
            repository.CommitChanges("Summary Message", "There is no data to comm it").Should().BeTrue();
            await repository.PushChanges(false,
                                         "Summary Message",
                                         "There is no data to comm it",
                                         new GitBranchName("arbitraryBranch1"),
                                         CancellationToken.None);
            await repository.PushChanges(false,
                                         "Summary Message",
                                         "There is no data to comm it",
                                         new GitBranchName("arbitraryBranch2"),
                                         CancellationToken.None);
        }

        [Test]
        public async Task WhenCreatingAPrThePrTitleAndBodyMatchTheCommitMessageFields()
        {
            string filename = "newFile.txt";
            File.WriteAllText(Path.Combine(RepositoryRootPath, filename), "");
            repository.StageFiles(new[] { filename });
            var commitSummary = "Summary Message";
            var commitDescription = "A commit description";
            repository.CommitChanges(commitSummary, commitDescription).Should().BeTrue();
            var prBranch = new GitBranchName("arbitraryBranch");
            await repository.PushChanges(true,
                                         commitSummary,
                                         commitDescription,
                                         prBranch,
                                         CancellationToken.None);
            await gitHubPullRequestCreator.Received(1)
                                          .CreatePullRequest(log,
                                                             gitConnection,
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
            
            repository.StageFiles(new[] { filename });
            repository.CommitChanges("Summary Message", "A file has changed").Should().BeTrue();
            
            // Act
            repository.Dispose();
            
            // Assert
            fileSystem.DirectoryExists(RepositoryRootPath)
                      .Should()
                      .BeFalse();
        }

        [Test]
        [TestCase("", 0)]
        [TestCase("./", 0)]
        [TestCase("nested_1", 2)]
        [TestCase("nested_1/nested_2", 3)]
        [TestCase("nested", 3)]
        [TestCase("nest", 4)]
        public void RemoveFiles(string subPath, int totalFilesRemaining)
        {
            //Arrange
            bareOrigin.AddFilesToBranch(branchName,
                                        ("file.yaml", ""),
                                        ("nested/file.txt", ""),
                                        ("nested_1/file.yaml", ""),
                                        ("nested_1/nested_2/file.yaml", ""));

            var repositoryFactory = new RepositoryFactory(log, fileSystem, tempDirectory, gitHubPullRequestCreator);
            gitConnection = new GitConnection(null, null, OriginPath, branchName);

            // Act
            var sut = repositoryFactory.CloneRepository($"{repositoryPath}/sut", gitConnection);
            sut.RecursivelyStageFilesForRemoval(subPath);
            sut.CommitChanges("Deleted files", string.Empty);
            sut.PushChanges(branchName.Value);

            //Assert
            var result = CloneOrigin();
            var files = fileSystem.EnumerateFilesWithGlob(result, "**/*");
            var notGitFiles = files.Where(file => !file.Contains(".git")).ToList();
            notGitFiles.Count.Should().Be(totalFilesRemaining);
        }

        string CloneOrigin()
        {
            var subPath = Guid.NewGuid().ToString();
            var resultPath = Path.Combine(tempDirectory, subPath);
            Repository.Clone(OriginPath, resultPath);
            var resultRepo = new Repository(resultPath);
            LibGit2Sharp.Commands.Checkout(resultRepo, $"origin/{branchName.Value}");

            return resultPath;
        }
    }
}
#endif