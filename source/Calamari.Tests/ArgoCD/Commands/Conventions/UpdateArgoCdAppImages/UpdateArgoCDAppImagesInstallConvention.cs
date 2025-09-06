using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.ArgoCD.Git;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions.UpdateArgoCdAppImages
{
    [TestFixture]
    public class UpdateArgoCDAppImagesInstallConvention
    {
        // This is a rough-copy of the ArgoCDAppImageUpdater tests from Octopus
        // readonly IArgoCDGitOpsRepository fakeArgoCDGitOpsRepository = Substitute.For<IArgoCDGitOpsRepository>();
        // readonly IGitOpsRepositoryFactory gitOpsRepositoryFactory = Substitute.For<IGitOpsRepositoryFactory>();
        
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        ILog log;
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        GitBranchName argoCdBranchName = new GitBranchName("devBranch");

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();

            Repository BareOrigin = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(argoCdBranchName, OriginPath);
        }
        
        [Test]
        public void UpdateImages_WithNoMatchingFiles_ReturnsEmptySet()
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
                [SpecialVariables.Git.BranchName("repo_name")] = argoCdBranchName.Value,
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
        //
        // [Test]
        // public async Task UpdateImages_WithNoImageMatches_ReturnsEmptySetAndCommitsNoChanges()
        // {
        //     // Arrange
        //     var updater = new ArgoCDAppImageUpdater(gitOpsRepositoryFactory);
        //
        //     var returnedFiles = new Dictionary<string, string>()
        //     {
        //         { "include/file1.yaml", "No yaml here" }
        //     };
        //     fakeArgoCDGitOpsRepository.ReadYamlFiles(Arg.Any<ITaskLog>(), Arg.Any<CancellationToken>()).Returns(returnedFiles);
        //
        //     var updateTarget = new ArgoCDImageUpdateTarget("App Name",
        //                                                    "docker.io",
        //                                                    "include",
        //                                                    new Uri("http://localhost/fake-repo.git"),
        //                                                    "branch");
        //
        //     var imagesToUpdate = new List<ContainerImageReference>
        //     {
        //         ContainerImageReference.FromReferenceString("nginx:1.27.1", "docker.io")
        //     };
        //
        //     // Act
        //     var result = await updater.UpdateImages(updateTarget,
        //                                             imagesToUpdate,
        //                                             new GitCommitSummary("Nothing"),
        //                                             string.Empty,
        //                                             false,
        //                                             log,
        //                                             CancellationToken);
        //
        //     // Assert
        //     result.ImagesUpdated.Should().BeEmpty();
        //     log.Received().Verbose("Processing file include/file1.yaml in Repository http://localhost/fake-repo.git.");
        //     log.Received().Verbose("No changes made to file include/file1.yaml as no image references were updated.");
        //
        // }
        //
        // [Test]
        // public async Task UpdateImages_WithImageMatches_CommitsChangesToGitAndReturnsUpdatedImages()
        // {
        //     // Arrange
        //     var updater = new ArgoCDAppImageUpdater(gitOpsRepositoryFactory);
        //
        //     var returnedFiles = new Dictionary<string, string>()
        //     {
        //         {
        //             "include/file1.yaml",
        //             """
        //             apiVersion: apps/v1
        //             kind: Deployment
        //             metadata:
        //               name: sample-deployment
        //             spec:
        //               replicas: 1
        //               selector:
        //                 matchLabels:
        //                   app: sample-deployment
        //               template:
        //                 metadata:
        //                   labels:
        //                     app: sample-deployment
        //                 spec:
        //                   containers:
        //                     - name: nginx
        //                       image: nginx:1.19 
        //                     - name: alpine
        //                       image: alpine:3.21 
        //             """
        //         }
        //     };
        //     var commitSummary = new GitCommitSummary("Commit Summary Here");
        //     const string userCommitDescription = "User provided commit description";
        //     const string updatedYamlContent =
        //         """
        //         apiVersion: apps/v1
        //         kind: Deployment
        //         metadata:
        //           name: sample-deployment
        //         spec:
        //           replicas: 1
        //           selector:
        //             matchLabels:
        //               app: sample-deployment
        //           template:
        //             metadata:
        //               labels:
        //                 app: sample-deployment
        //             spec:
        //               containers:
        //                 - name: nginx
        //                   image: nginx:1.27.1 
        //                 - name: alpine
        //                   image: alpine:3.21 
        //         """;
        //
        //
        //     fakeArgoCDGitOpsRepository.ReadYamlFiles(log, Arg.Any<CancellationToken>()).Returns(returnedFiles);
        //     fakeArgoCDGitOpsRepository.TryCommitChanges(Arg.Is<ImageUpdateChanges>(i => i.UpdatedImageReferences.Contains("nginx:1.27.1")
        //                                                                                 && i.UpdatedFiles.ContainsKey("include/file1.yaml")
        //                                                                                 && i.UpdatedFiles["include/file1.yaml"] == updatedYamlContent),
        //                                                 commitSummary,
        //                                                 userCommitDescription,
        //                                                 log,
        //                                                 Arg.Any<CancellationToken>())
        //                               .Returns(true);
        //
        //     var updateTarget = new ArgoCDImageUpdateTarget("App Name",
        //                                                    "docker.io",
        //                                                    "include",
        //                                                    new Uri("http://localhost/fake-repo.git"),
        //                                                    "branch");
        //
        //     var imagesToUpdate = new List<ContainerImageReference>
        //     {
        //         ContainerImageReference.FromReferenceString("nginx:1.27.1", "docker.io")
        //     };
        //
        //     // Act
        //     var result = await updater.UpdateImages(updateTarget,
        //                                             imagesToUpdate,
        //                                             commitSummary,
        //                                             userCommitDescription,
        //                                             false,
        //                                             log,
        //                                             CancellationToken);
        //
        //     // Assert
        //     result.ImagesUpdated.Should().BeEquivalentTo(["nginx:1.27.1"]);
        //     await fakeArgoCDGitOpsRepository.Received()
        //     .TryCommitChanges(Arg.Any<ImageUpdateChanges>(),
        //                       commitSummary,
        //                       userCommitDescription,
        //                       log,
        //                       Arg.Any<CancellationToken>());
        // }
        //
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
