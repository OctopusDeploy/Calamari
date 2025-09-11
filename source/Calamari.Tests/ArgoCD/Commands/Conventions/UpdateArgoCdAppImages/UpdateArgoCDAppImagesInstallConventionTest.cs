#if NET
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
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
        NonSensitiveCalamariVariables nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables();
        
        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser = Substitute.For<IArgoCDApplicationManifestParser>();
        readonly ICustomPropertiesLoader customPropertiesLoader = Substitute.For<ICustomPropertiesLoader>();

        [SetUp]
        public void Init()
        {
            tempDirectory = fileSystem.CreateTemporaryDirectory();

            originRepo = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(argoCDBranchName, OriginPath);
            
            nonSensitiveCalamariVariables.Add(SpecialVariables.Git.CommitMessageSummary, "Commit Summary");
            nonSensitiveCalamariVariables.Add(SpecialVariables.Git.CommitMessageDescription, "Commit Description");
            
            var argoCdCustomPropertiesDto = new ArgoCDCustomPropertiesDto(new[]
            {
                new ArgoCDApplicationDto("Gateway1", "App1", "docker.io",new[]
                {
                    new ArgoCDApplicationSourceDto(OriginPath, "", argoCDBranchName.Value, "Directory")
                }, "yaml")
            }, new GitCredentialDto[]
            {
                new GitCredentialDto(new Uri(OriginPath).AbsoluteUri, "", "")
            });
            customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>().Returns(argoCdCustomPropertiesDto);
            
            var argoCdApplicationFromYaml = new Application()
            {
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>()
                    {
                        new BasicSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            Path = "",
                            TargetRevision = argoCDBranchName.Value
                        }  
                    } 
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);
        }

        [Test]
        public void UpdateImages_WithNoMatchingFiles_ReturnsEmptySet()
        {
            // Arrange
            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     Substitute.For<IGitHubPullRequestCreator>(),
                                                                     fileSystem,
                                                                     new ArgoCommitToGitConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader, argoCdApplicationManifestParser);
            var variables = new CalamariVariables
            {
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
            var ignoredGitSubfolder = filesInRepo.Where(file => !file.Contains(".git"));
            ignoredGitSubfolder.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithNoImageMatches_ReturnsEmptySetAndCommitsNoChanges()
        {
            // Arrange
            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     Substitute.For<IGitHubPullRequestCreator>(),
                                                                     fileSystem,
                                                                     new ArgoCommitToGitConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader, argoCdApplicationManifestParser);
            var variables = new CalamariVariables
            {
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
            log.StandardOut.Should().Contain(s => s.Contains($"Processing file include{Path.DirectorySeparatorChar}file1.yaml"));
            log.StandardOut.Should().Contain($"No changes made to file include{Path.DirectorySeparatorChar}file1.yaml as no image references were updated.");

            var resultRepo = CloneOrigin();
            var repoFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "include/file1.yaml"));
            repoFileContent.Should().Be("No Yaml here");
        }

        [Test]
        public void UpdateImages_WithImageMatches_CommitsChangesToGitAndReturnsUpdatedImages()
        {
            // Arrange
            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     Substitute.For<IGitHubPullRequestCreator>(),
                                                                     fileSystem,
                                                                     new ArgoCommitToGitConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader, argoCdApplicationManifestParser);
            var variables = new CalamariVariables
            {
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