using System;
using System.IO;
using Calamari.ArgoCD.Commands;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
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

            Repository BareOrigin = RepositoryHelpers.CreateBareRepository(OriginPath);
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
            const string nestedFilename = "nested/second.yaml";
            const string firstFileContent = "firstContent";
            const string secondFileContent = "secondContent";
            
            var nestedDirectory = Path.Combine(PackageDirectory, "nested");
            Directory.CreateDirectory(nestedDirectory);

            
            File.WriteAllText(Path.Combine(PackageDirectory, firstFilename), firstFileContent);
            File.WriteAllText(Path.Combine(PackageDirectory, nestedFilename), secondFileContent);
            
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "./*\nnested/*",
                [SpecialVariables.Git.SubFolder("repo_name")] = "",
                [SpecialVariables.Git.Password("repo_name")] = "password",
                [SpecialVariables.Git.Username("repo_name")] = "username",
                [SpecialVariables.Git.Url("repo_name")] = OriginPath,
                [SpecialVariables.Git.BranchName("repo_name")] = argoCdBranchName.Value,
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this"
            };
            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;
            
            var convention = new UpdateGitRepositoryInstallConvention(fileSystem, CommitToGitCommand.PackageDirectoryName, log, Substitute.For<IGitHubPullRequestCreator>());
            
            convention.Install(runningDeployment);
            
            var resultPath = Path.Combine(tempDirectory, "result");
            Repository.Clone(OriginPath, resultPath);
            var resultRepo = new Repository(resultPath);
            LibGit2Sharp.Commands.Checkout(resultRepo, $"origin/{argoCdBranchName}");
            
            var resultFirstContent = File.ReadAllText(Path.Combine(resultPath, firstFilename));
            var resultNestedContent = File.ReadAllText(Path.Combine(resultPath, nestedFilename));
            resultFirstContent.Should().Be(firstFileContent);
            resultNestedContent.Should().Be(secondFileContent);
            Console.WriteLine(log.ToString());
        }
    }
}