#if NET
using System;
using System.IO;
using Calamari.ArgoCD.Commands;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.ArgoCD.Git;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using LibGit2Sharp;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class UpdateGitRepositoryInstallConventionTests
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        InMemoryLog log;
        string tempDirectory;
        string WorkingDirectory => Path.Combine(tempDirectory, "working");
        string PackageDirectory => Path.Combine(WorkingDirectory, CommitToGitCommand.PackageDirectoryName);
        
        string OriginPath => Path.Combine(tempDirectory, "origin");

        GitBranchName argoCdBranchName = new GitBranchName("devBranch");

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();
            Directory.CreateDirectory(PackageDirectory);

            RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(argoCdBranchName, OriginPath);
        }
        
        [TearDown]
        public void Cleanup()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void ExecuteCopiesFilesFromPackageIntoRepo()
        {
            const string firstFilename = "first.yaml";
            CreateFileUnderPackageDirectory(firstFilename);
            const string nestedFilename = "nested/second.yaml";
            CreateFileUnderPackageDirectory(nestedFilename);
            
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.Recursive] = "True",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this"
            };
            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;

            var customPropertiesLoader = SetupCustomPropertiesLoader();

            var convention = new UpdateGitRepositoryInstallConvention(fileSystem, CommitToGitCommand.PackageDirectoryName, log, Substitute.For<IGitHubPullRequestCreator>(), new ArgoCommitToGitConfigFactory(log), customPropertiesLoader);
            convention.Install(runningDeployment);

            var resultPath = CloneOrigin();
            var resultFirstContent = File.ReadAllText(Path.Combine(resultPath, firstFilename));
            var resultNestedContent = File.ReadAllText(Path.Combine(resultPath, nestedFilename));
            resultFirstContent.Should().Be(firstFilename);
            resultNestedContent.Should().Be(nestedFilename);
        }

        ICustomPropertiesLoader SetupCustomPropertiesLoader()
        {
            var customPropertiesFactory = Substitute.For<ICustomPropertiesLoader>();
            customPropertiesFactory.Load<ArgoCDCustomPropertiesDto>().Returns(new ArgoCDCustomPropertiesDto(new[]
            {
                new ArgoCDApplicationDto("Gateway1", "App1", "docker.io", new[]
                {
                    new ArgoCDApplicationSourceDto(OriginPath, "username", "password", argoCdBranchName.Value, "")
                })
            }));
            return customPropertiesFactory;
        }

        [Test]
        public void DoesNotCopyFilesRecursivelyIfNotSet()
        {
            const string firstFilename = "first.yaml";
            CreateFileUnderPackageDirectory(firstFilename);
            const string nestedFilename = "nested/second.yaml";
            CreateFileUnderPackageDirectory(nestedFilename);
            
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.Recursive] = "False",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this"
            };
            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;    
            
            var customPropertiesLoader = SetupCustomPropertiesLoader();

            var convention = new UpdateGitRepositoryInstallConvention(fileSystem, CommitToGitCommand.PackageDirectoryName, log, Substitute.For<IGitHubPullRequestCreator>(), new ArgoCommitToGitConfigFactory(log), customPropertiesLoader);
            convention.Install(runningDeployment);
            
            var resultPath = CloneOrigin();
            File.Exists(Path.Combine(resultPath, firstFilename)).Should().BeTrue();
            File.Exists(Path.Combine(resultPath, nestedFilename)).Should().BeFalse();
        }

        //Accepts a relative path and creates a file under the package directory, which
        //contains the relative filename as its contents.
        void CreateFileUnderPackageDirectory(string filename)
        {
            var packageFile = Path.Combine(PackageDirectory, filename);
            var directory = Path.GetDirectoryName(packageFile);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);        
            }

            File.WriteAllText(packageFile, filename);
        }

        string CloneOrigin()
        {
            var subPath = Guid.NewGuid().ToString();
            var resultPath = Path.Combine(tempDirectory, subPath);
            Repository.Clone(OriginPath, resultPath);
            var resultRepo = new Repository(resultPath);
            LibGit2Sharp.Commands.Checkout(resultRepo, $"origin/{argoCdBranchName}");

            return resultPath;
        }
    }
}
#endif 