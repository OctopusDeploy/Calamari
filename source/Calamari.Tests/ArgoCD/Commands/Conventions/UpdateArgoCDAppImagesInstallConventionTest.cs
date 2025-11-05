#if NET
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
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
    public class UpdateArgoCDAppImagesInstallConventionTests
    {
        const string ProjectSlug = "TheProject";
        const string EnvironmentSlug = "TheEnvironment";

        // This is a rough-copy of the ArgoCDAppImageUpdater tests from Octopus

        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        readonly InMemoryLog log = new InMemoryLog();
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        Repository originRepo;
        
        const string ArgoCDBranchFriendlyName = "devBranch";
        readonly GitBranchName argoCDBranchName = GitBranchName.CreateFromFriendlyName(ArgoCDBranchFriendlyName);
        readonly NonSensitiveCalamariVariables nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables();
        
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
                new ArgoCDApplicationDto("Gateway1", "App1", "argocd",new[]
                {
                    new ArgoCDApplicationSourceDto(OriginPath, "", ArgoCDBranchFriendlyName)
                }, "yaml", "docker.io","http://my-argo.com")
            }, new GitCredentialDto[]
            {
                new GitCredentialDto(new Uri(OriginPath).AbsoluteUri, "", "")
            });
            customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>().Returns(argoCdCustomPropertiesDto);

            var argoCdApplicationFromYaml = new ArgoCDApplicationBuilder()
                                            .WithName("The app")
                                            .WithAnnotations(new Dictionary<string, string>()
                                            {
                                                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                            })
                                            .WithSource(new BasicSource()
                                            {
                                                RepoUrl = new Uri(OriginPath),
                                                Path = "",
                                                TargetRevision = ArgoCDBranchFriendlyName
                                            })
                                            .Build();
            
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);
        }

        [Test]
        public void UpdateImages_WithNoMatchingFiles_ReturnsEmptySet()
        {
            // Arrange
            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader, 
                                                                     argoCdApplicationManifestParser, 
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [PackageVariables.IndexedImage("nginx")] = "index.docker.io/nginx:1.27.1",
                [PackageVariables.IndexedPackagePurpose("nginx")] = "DockerImageReference",
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
            };
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            // Act
            updater.Install(runningDeployment);

            // Assert
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var filesInRepo = fileSystem.EnumerateFilesRecursively(resultRepo, "*");
            var ignoredGitSubfolder = filesInRepo.Where(file => !file.Contains(".git"));
            ignoredGitSubfolder.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithNoImageMatches_ReturnsEmptySetAndCommitsNoChanges()
        {
            // Arrange
            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader, argoCdApplicationManifestParser, Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [PackageVariables.IndexedImage("nginx")] = "docker.io/nginx:1.27.1",
                [PackageVariables.IndexedPackagePurpose("nginx")] = "DockerImageReference",
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
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

            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var repoFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "include/file1.yaml"));
            repoFileContent.Should().Be("No Yaml here");
        }

        [Test]
        public void UpdateImages_WithImageMatches_CommitsChangesToGitAndReturnsUpdatedImages()
        {
            // Arrange
            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader, 
                                                                     argoCdApplicationManifestParser, 
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [PackageVariables.IndexedImage("nginx")] = "index.docker.io/nginx:1.27.1",
                [PackageVariables.IndexedPackagePurpose("nginx")] = "DockerImageReference",
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
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

            // Act
            updater.Install(runningDeployment);


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
            
            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var fileInRepo = Path.Combine(clonedRepoPath, existingYamlFile);
            fileSystem.FileExists(fileInRepo).Should().BeTrue();
            var content = fileSystem.ReadFile(fileInRepo);
            content.Should().Be(updatedYamlContent);
        }

        [Test]
        public void UpdateImages_ForKustomizeFileWorks()
        {
            // Arrange
            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [PackageVariables.IndexedImage("nginx")] = "index.docker.io/nginx:1.27.1",
                [PackageVariables.IndexedPackagePurpose("nginx")] = "DockerImageReference",
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
            };
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            var kustomizeFile = "kustomization.yaml";
            var filesInRepo = new (string, string)[]
            {
                (kustomizeFile,
                 @"
images:
- name: ""docker.io/nginx""
  newTag: ""1.25""
")
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);
            
            // Act
            updater.Install(runningDeployment);
            
            // Assert
            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var fileInRepo = Path.Combine(clonedRepoPath, kustomizeFile);
            fileSystem.FileExists(fileInRepo).Should().BeTrue();
            var content = fileSystem.ReadFile(fileInRepo);
            content.Should().Contain("1.27.1");
        }
    }
}

#endif