#if NET
using System;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
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

namespace Calamari.Tests.ArgoCD.Commands.Conventions.UpdateArgoCdAppImages
{
    [TestFixture]
    public class UpdateArgoCDAppImagesInstallConventionTests
    {
        // This is a rough-copy of the ArgoCDAppImageUpdater tests from Octopus

        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        readonly InMemoryLog log = new InMemoryLog();
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        Repository originRepo;
        GitBranchName argoCDBranchName = new GitBranchName("devBranch");

        [SetUp]
        public void Init()
        {
            tempDirectory = fileSystem.CreateTemporaryDirectory();

            originRepo = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(argoCDBranchName, OriginPath);
        }

        [Test]
        public void UpdateImages_WithNoMatchingFiles_ReturnsEmptySet()
        {
            // Arrange
            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     Substitute.For<IGitHubPullRequestCreator>(),
                                                                     fileSystem,
                                                                     new ArgoCommitToGitConfigFactory(log),
                                                                     new CommitMessageGenerator());
            var variables = new CalamariVariables
            {
                [SpecialVariables.Git.SubFolder("repo_name")] = "",
                [SpecialVariables.Git.Password("repo_name")] = "password",
                [SpecialVariables.Git.Username("repo_name")] = "username",
                [SpecialVariables.Git.Url("repo_name")] = OriginPath,
                [SpecialVariables.Git.BranchName("repo_name")] = argoCDBranchName.Value,
                [SpecialVariables.Git.DefaultRegistry("repo_name")] = "docker.io",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this",
                [Deployment.SpecialVariables.Packages.Image("nginx")] = "nginx:1.27.1",
                [Deployment.SpecialVariables.Packages.Purpose("nginx")] = "DockerImageReference",
            };
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            // Act
            updater.Install(runningDeployment);

            // Assert
            var resultRepo = CloneOrigin();
            var filesInRepo = fileSystem.EnumerateFilesRecursively(resultRepo, "*");
            filesInRepo.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithNoImageMatches_ReturnsEmptySetAndCommitsNoChanges()
        {
            // Arrange
            var updater = new Calamari.ArgoCD.Conventions.UpdateArgoCDAppImagesInstallConvention(log,
                                                                                                 Substitute.For<IGitHubPullRequestCreator>(),
                                                                                                 fileSystem,
                                                                                                 new ArgoCommitToGitConfigFactory(log),
                                                                                                 new CommitMessageGenerator());
            var variables = new CalamariVariables
            {
                [SpecialVariables.Git.SubFolder("repo_name")] = "",
                [SpecialVariables.Git.Password("repo_name")] = "password",
                [SpecialVariables.Git.Username("repo_name")] = "username",
                [SpecialVariables.Git.Url("repo_name")] = OriginPath,
                [SpecialVariables.Git.BranchName("repo_name")] = argoCDBranchName.Value,
                [SpecialVariables.Git.DefaultRegistry("repo_name")] = "docker.io",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this",
                [Deployment.SpecialVariables.Packages.Image("nginx")] = "nginx:1.27.1",
                [Deployment.SpecialVariables.Packages.Purpose("nginx")] = "DockerImageReference",
            };
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            originRepo.AddFilesToBranch(argoCDBranchName, ("include/file1.yaml", "No Yaml here"));

            // Act
            updater.Install(runningDeployment);

            // Assert
            log.StandardOut.Should().Contain(s => s.Contains($"Processing file include/file1.yaml"));
            log.StandardOut.Should().Contain("No changes made to file include/file1.yaml as no image references were updated.");
            
            var resultRepo = CloneOrigin();
            var repoFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "include/file1.yaml"));
            repoFileContent.Should().Be("No Yaml here");

        }

        [Test]
        public void UpdateImages_WithImageMatches_CommitsChangesToGitAndReturnsUpdatedImages()
        {
            // Arrange
            var updater = new Calamari.ArgoCD.Conventions.UpdateArgoCDAppImagesInstallConvention(log,
                                                                                                 Substitute.For<IGitHubPullRequestCreator>(),
                                                                                                 fileSystem,
                                                                                                 new ArgoCommitToGitConfigFactory(log),
                                                                                                 new CommitMessageGenerator());
            var variables = new CalamariVariables
            {
                [SpecialVariables.Git.SubFolder("repo_name")] = "",
                [SpecialVariables.Git.Password("repo_name")] = "password",
                [SpecialVariables.Git.Username("repo_name")] = "username",
                [SpecialVariables.Git.Url("repo_name")] = OriginPath,
                [SpecialVariables.Git.BranchName("repo_name")] = argoCDBranchName.Value,
                [SpecialVariables.Git.DefaultRegistry("repo_name")] = "docker.io",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this",
                [Deployment.SpecialVariables.Packages.Image("nginx")] = "nginx:1.27.1",
                [Deployment.SpecialVariables.Packages.Purpose("nginx")] = "DockerImageReference",
            };
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            var existingYamlFile = "include/file1.yaml";
            var filesInRepo = new (string, string)[]
            {
                (
                    existingYamlFile,
                    @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: sample-deployment
spec:
  replicas: 1
  selector:
    matchLabels:
      app: sample-deployment
  template:
    metadata:
      labels:
        app: sample-deployment
    spec:
      containers:
        - name: nginx
          image: nginx:1.19 
        - name: alpine
          image: alpine:3.21 
"
                )
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            //Assert
            const string updatedYamlContent =
                @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: sample-deployment
spec:
  replicas: 1
  selector:
    matchLabels:
      app: sample-deployment
  template:
    metadata:
      labels:
        app: sample-deployment
    spec:
      containers:
        - name: nginx
          image: nginx:1.27.1 
        - name: alpine
          image: alpine:3.21 
";
            // Act
            updater.Install(runningDeployment);

            // Assert
            var clonedRepoPath = CloneOrigin();
            var fileInRepo = Path.Combine(clonedRepoPath, existingYamlFile);
            fileSystem.FileExists(fileInRepo).Should().BeTrue();
            var content = fileSystem.ReadFile(fileInRepo);
            content.Should().Be(updatedYamlContent);
        }

        string CloneOrigin()
        {
            var subPath = Guid.NewGuid().ToString();
            var resultPath = Path.Combine(tempDirectory, subPath);
            Repository.Clone(OriginPath, resultPath);
            var resultRepo = new Repository(resultPath);
            LibGit2Sharp.Commands.Checkout(resultRepo, $"origin/{argoCDBranchName}");

            return resultPath;
        }
    }
}

#endif