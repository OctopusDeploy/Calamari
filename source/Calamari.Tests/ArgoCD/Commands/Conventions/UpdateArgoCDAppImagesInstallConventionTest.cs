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
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Time;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.ArgoCD.Git;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using FluentAssertions.Execution;
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
        InMemoryLog log;
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        Repository originRepo;

        const string ArgoCDBranchFriendlyName = "devBranch";
        const string GatewayId = "Gateway1";
        readonly GitBranchName argoCDBranchName = GitBranchName.CreateFromFriendlyName(ArgoCDBranchFriendlyName);
        readonly NonSensitiveCalamariVariables nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables();

        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser = Substitute.For<IArgoCDApplicationManifestParser>();
        readonly ICustomPropertiesLoader customPropertiesLoader = Substitute.For<ICustomPropertiesLoader>();
        IArgoCDFilesUpdatedReporter deploymentReporter;

        UpdateArgoCDAppImagesInstallConvention CreateConvention()
        {
            return new UpdateArgoCDAppImagesInstallConvention(
                log,
                fileSystem,
                new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                new CommitMessageGenerator(),
                customPropertiesLoader,
               argoCdApplicationManifestParser,
                Substitute.For<IGitVendorAgnosticApiAdapterFactory>(),
                new SystemClock(),
                deploymentReporter);
        }

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            deploymentReporter = Substitute.For<IArgoCDFilesUpdatedReporter>();
            tempDirectory = fileSystem.CreateTemporaryDirectory();

            originRepo = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(argoCDBranchName, OriginPath);

            nonSensitiveCalamariVariables.Add(SpecialVariables.Git.CommitMessageSummary, "Commit Summary");
            nonSensitiveCalamariVariables.Add(SpecialVariables.Git.CommitMessageDescription, "Commit Description");

            var argoCdCustomPropertiesDto = new ArgoCDCustomPropertiesDto(
                                                                          [
                                                                              new ArgoCDGatewayDto(GatewayId, "Gateway1")
                                                                          ],
                                                                          [
                                                                              new ArgoCDApplicationDto(GatewayId,
                                                                                                       "App1",
                                                                                                       "argocd",
                                                                                                       "yaml",
                                                                                                       "docker.io",
                                                                                                       "http://my-argo.com")
                                                                          ],
                                                                          [
                                                                              new GitCredentialDto(new Uri(OriginPath).AbsoluteUri, "", "")
                                                                          ]);
            customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>().Returns(argoCdCustomPropertiesDto);

            var argoCdApplicationFromYaml = new ArgoCDApplicationBuilder()
                                            .WithName("App1")
                                            .WithAnnotations(new Dictionary<string, string>()
                                            {
                                                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                            })
                                            .WithSource(new ApplicationSource()
                                            {
                                                OriginalRepoUrl = OriginPath,
                                                Path = "",
                                                TargetRevision = ArgoCDBranchFriendlyName,
                                            }, SourceTypeConstants.Directory)
                                            .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);
        }

        [Test]
        public void PluginSourceType_DontUpdate()
        {
            // Arrange
            var updater = CreateConvention();
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
            var kustomizeFileContents = @"
images:
- name: ""docker.io/nginx""
  newTag: ""1.25""
";
            var filesInRepo = new (string, string)[]
            {
                (kustomizeFile,
                 kustomizeFileContents)
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            var argoCdApplicationFromYaml = new ArgoCDApplicationBuilder()
                                            .WithName("App1")
                                            .WithAnnotations(new Dictionary<string, string>()
                                            {
                                                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                            })
                                            .WithSource(new ApplicationSource()
                                            {
                                                OriginalRepoUrl = OriginPath,
                                                Path = "",
                                                TargetRevision = ArgoCDBranchFriendlyName,
                                            }, SourceTypeConstants.Plugin)
                                            .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            // Act
            updater.Install(runningDeployment);

            // Assert
            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            AssertFileContents(clonedRepoPath, kustomizeFile, kustomizeFileContents);

            log.MessagesWarnFormatted.Should().Contain("Unable to update source 'Index: 0, Type: Plugin, Name: (None)' as Plugin sources aren't currently supported.");

            AssertOutputVariables(false);
        }

        [Test]
        public void DirectorySource_NoMatchingFiles_DontUpdate()
        {
            // Arrange
            var updater = CreateConvention();
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

            AssertOutputVariables(false);
        }

        [Test]
        public void DirectorySource_NoImageMatches_DontUpdate()
        {
            // Arrange
            var updater = CreateConvention();
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
            AssertFileContents(resultRepo, "include/file1.yaml", "No Yaml here");

            AssertOutputVariables(false);
        }

        [Test]
        public void DirectorySource_ImageMatches_Update()
        {
            // Arrange
            var updater = CreateConvention();
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

            var yamlFilename = "include/file1.yaml";
            var filesInRepo = new (string, string)[]
            {
                (
                    yamlFilename,
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
            AssertFileContents(clonedRepoPath, yamlFilename, updatedYamlContent);

            AssertOutputVariables();
        }

        [Test]
        public void DirectorySource_NoPath_DontUpdate()
        {
            // Arrange
            var updater = CreateConvention();
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

            var yamlFilename = "include/file1.yaml";
            var fileContents = @"
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
";
            var filesInRepo = new (string, string)[]
            {
                (
                    yamlFilename,
                    fileContents
                )
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            var argoCdApplicationFromYaml = new ArgoCDApplicationBuilder()
                                            .WithName("App1")
                                            .WithAnnotations(new Dictionary<string, string>()
                                            {
                                                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                            })
                                            .WithSource(new ApplicationSource()
                                            {
                                                OriginalRepoUrl = OriginPath,
                                                TargetRevision = ArgoCDBranchFriendlyName,
                                            }, SourceTypeConstants.Directory)
                                            .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            // Act
            updater.Install(runningDeployment);

            //Assert
            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            AssertFileContents(clonedRepoPath, yamlFilename, fileContents);

            log.MessagesWarnFormatted.Should().Contain("Unable to update source 'Index: 0, Type: Directory, Name: (None)' as a path has not been specified.");

            AssertOutputVariables(false);
        }

        [Test]
        public void KustomizeSource_NoPath_DontUpdate()
        {
            // Arrange
            var updater = CreateConvention();
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
            var kustomizeFileContents = @"
images:
- name: ""docker.io/nginx""
  newTag: ""1.25""
";
            var filesInRepo = new (string, string)[]
            {
                (kustomizeFile,
                 kustomizeFileContents)
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            var argoCdApplicationFromYaml = new ArgoCDApplicationBuilder()
                                            .WithName("App1")
                                            .WithAnnotations(new Dictionary<string, string>()
                                            {
                                                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                            })
                                            .WithSource(new ApplicationSource()
                                            {
                                                OriginalRepoUrl = OriginPath,
                                                TargetRevision = ArgoCDBranchFriendlyName,
                                            }, SourceTypeConstants.Kustomize)
                                            .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            // Act
            updater.Install(runningDeployment);

            // Assert

            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            AssertFileContents(clonedRepoPath, kustomizeFile, kustomizeFileContents);

            log.MessagesWarnFormatted.Should().Contain("Unable to update source 'Index: 0, Type: Kustomize, Name: (None)' as a path has not been specified.");

            AssertOutputVariables(false);
        }

         [Test]
        public void KustomizeSource_HasKustomizationFile_Update()
        {
            // Arrange
            var updater = CreateConvention();
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

            var argoCdApplicationFromYaml = new ArgoCDApplicationBuilder()
                                            .WithName("App1")
                                            .WithAnnotations(new Dictionary<string, string>()
                                            {
                                                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                            })
                                            .WithSource(new ApplicationSource()
                                            {
                                                OriginalRepoUrl = OriginPath,
                                                Path = "",
                                                TargetRevision = ArgoCDBranchFriendlyName,
                                            }, SourceTypeConstants.Kustomize)
                                            .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            // Act
            updater.Install(runningDeployment);

            // Assert
            var updatedYamlContent = @"
images:
- name: ""docker.io/nginx""
  newTag: ""1.27.1""
";
            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            AssertFileContents(clonedRepoPath, kustomizeFile, updatedYamlContent);

            AssertOutputVariables();
        }

        [Test]
        public void KustomizeSource_NoKustomizationFile_DontUpdate()
        {
            // Arrange
            var updater = CreateConvention();
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
            var existingYamlContent = @"
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
";
            var filesInRepo = new (string, string)[]
            {
                (
                    existingYamlFile,
                    existingYamlContent
                )
            };

            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            var argoCdApplicationFromYaml = new ArgoCDApplicationBuilder()
                                            .WithName("App1")
                                            .WithAnnotations(new Dictionary<string, string>()
                                            {
                                                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                            })
                                            .WithSource(new ApplicationSource()
                                            {
                                                OriginalRepoUrl = OriginPath,
                                                Path = "",
                                                TargetRevision = ArgoCDBranchFriendlyName,
                                            }, SourceTypeConstants.Kustomize)
                                            .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            // Act
            updater.Install(runningDeployment);

            // Assert
            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            AssertFileContents(clonedRepoPath, existingYamlFile, existingYamlContent);

            log.MessagesWarnFormatted.Should().Contain("kustomization file not found, no files will be updated");

            AssertOutputVariables(updated: false);
        }

        [Test]
        public void DirectorySource_ImageMatches_ReportsDeploymentWithNonEmptyCommitSha()
        {
            // Arrange
            var updater = CreateConvention();
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

            var yamlFilename = "include/file1.yaml";
            var filesInRepo = new (string, string)[]
            {
                (
                    yamlFilename,
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

            // Assert
            deploymentReporter.Received(1).ReportDeployments(Arg.Is<IReadOnlyList<ProcessApplicationResult>>(results =>
                results.Count == 1));
        }

        void AssertFileContents(string clonedRepoPath, string relativeFilePath, string expectedContent)
        {
            var absolutePath = Path.Combine(clonedRepoPath, relativeFilePath);
            fileSystem.FileExists(absolutePath).Should().BeTrue();

            var content = fileSystem.ReadFile(absolutePath);
            content.ReplaceLineEndings().Should().Be(expectedContent.ReplaceLineEndings());
        }

        void AssertOutputVariables(bool updated = true, string matchingApplicationTotalSourceCounts = "1")
        {
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");
            serviceMessages.GetPropertyValue("ArgoCD.GatewayIds").Should().Be(GatewayId);
            serviceMessages.GetPropertyValue("ArgoCD.GitUris").Should().Be(updated ? OriginPath : string.Empty);
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedImages").Should().Be(updated ? "1" : "0");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplications").Should().Be("App1");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationTotalSourceCounts").Should().Be(matchingApplicationTotalSourceCounts);
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationMatchingSourceCounts").Should().Be("1");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplications").Should().Be(updated ? "App1" : string.Empty);
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplicationSourceCounts").Should().Be(updated ? "1" : string.Empty);
        }
    }
}

