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
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
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

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();

            originRepo = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(argoCDBranchName, OriginPath);

            nonSensitiveCalamariVariables.Add(SpecialVariables.Git.CommitMessageSummary, "Commit Summary");
            nonSensitiveCalamariVariables.Add(SpecialVariables.Git.CommitMessageDescription, "Commit Description");

            var argoCdCustomPropertiesDto = new ArgoCDCustomPropertiesDto(new[]
                                                                          {
                                                                              new ArgoCDApplicationDto(GatewayId,
                                                                                                       "App1",
                                                                                                       "argocd",
                                                                                                       "yaml",
                                                                                                       "docker.io",
                                                                                                       "http://my-argo.com")
                                                                          },
                                                                          new GitCredentialDto[]
                                                                          {
                                                                              new GitCredentialDto(new Uri(OriginPath).AbsoluteUri, "", "")
                                                                          });
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
                                                RepoUrl = new Uri(OriginPath),
                                                Path = "",
                                                TargetRevision = ArgoCDBranchFriendlyName,
                                            }, SourceTypeConstants.Directory)
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
            
            AssertOutputVariables(false);
        }

        [Test]
        public void UpdateImages_WithNoImageMatches_ReturnsEmptySetAndCommitsNoChanges()
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
            
            AssertOutputVariables(false);
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
            content.ReplaceLineEndings().Should().Be(updatedYamlContent.ReplaceLineEndings());
            
            AssertOutputVariables();
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

            var argoCdApplicationFromYaml = new ArgoCDApplicationBuilder()
                                            .WithName("App1")
                                            .WithAnnotations(new Dictionary<string, string>()
                                            {
                                                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                            })
                                            .WithSource(new ApplicationSource()
                                            {
                                                RepoUrl = new Uri(OriginPath),
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
            var fileInRepo = Path.Combine(clonedRepoPath, kustomizeFile);
            fileSystem.FileExists(fileInRepo).Should().BeTrue();
            var content = fileSystem.ReadFile(fileInRepo);
            content.Should().Contain("1.27.1");

            AssertOutputVariables();
        }

        [Test]
        public void UpdateImages_RefWithHelmImageMatches_CommitsChangesToGitAndReturnsUpdatedImages()
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

            var existingYamlFile = "otherRepoPath/values.yaml";
            var filesInRepo = new (string, string)[]
            {
                (
                    existingYamlFile,
                    @"
image:
  repository: index.docker.io/nginx
  tag: ""1.0""
containerPort: 8080
service:
  type: LoadBalancer
"
                )
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            var argoCDAppWithHelmSource = new ArgoCDApplicationBuilder()
                                          .WithName("App1")
                                          .WithAnnotations(new Dictionary<string, string>()
                                          {
                                              [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("ref-source"))] = ProjectSlug,
                                              [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("ref-source"))] = EnvironmentSlug,
                                              [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(new ApplicationSourceName("helm-source"))] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                                          })
                                          .WithSource(new ApplicationSource
                                          {
                                              RepoUrl = new Uri("https://github.com/org/repo"),
                                              Path = "",
                                              TargetRevision = ArgoCDBranchFriendlyName,
                                              Helm = new HelmConfig
                                              {
                                                  ValueFiles = new List<string>()
                                                  {
                                                      "$values/otherRepoPath/values.yaml"
                                                  }
                                              },
                                              Name = "helm-source",
                                          }, SourceTypeConstants.Helm)
                                          .WithSource(new ApplicationSource
                                          {
                                              Name = "ref-source",
                                              Ref = "values",
                                              TargetRevision = ArgoCDBranchFriendlyName,
                                              RepoUrl = new Uri(OriginPath),
                                          }, SourceTypeConstants.Directory)
                                          .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCDAppWithHelmSource);
            // Act
            updater.Install(runningDeployment);

            //Assert
            const string updatedYamlContent =
                @"
image:
  repository: index.docker.io/nginx
  tag: ""1.27.1""
containerPort: 8080
service:
  type: LoadBalancer
";

            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var fileInRepo = Path.Combine(clonedRepoPath, existingYamlFile);
            fileSystem.FileExists(fileInRepo).Should().BeTrue();
            var content = fileSystem.ReadFile(fileInRepo);
            content.ReplaceLineEndings().Should().Be(updatedYamlContent.ReplaceLineEndings());

            AssertOutputVariables(matchingApplicationTotalSourceCounts: "2");
        }
       
        [Test]
        public void UpdateImages_RefWithHelmImageMatchesAndPath_IgnoresFilesUnderPath()
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

            var yamlFileUnderPath = "include/file1.yaml";
            var existingYamlFile = "otherRepoPath/values.yaml";
            var contentUnderPath = @"
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
            var filesInRepo = new[]
            {
                (
                    existingYamlFile,
                    @"
image:
  repository: index.docker.io/nginx
  tag: ""1.0""
containerPort: 8080
service:
  type: LoadBalancer
"
                ),
                (
                    yamlFileUnderPath,
                    contentUnderPath
                )
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);
            
            var argoCDAppWithHelmSource = new ArgoCDApplicationBuilder()
                                          .WithName("App1")
                                          .WithAnnotations(new Dictionary<string, string>()
                                          {
                                              [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("ref-source"))] = ProjectSlug,
                                              [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("ref-source"))] = EnvironmentSlug,
                                              [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(new ApplicationSourceName("helm-source"))] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                                          })
                                          .WithSource(new ApplicationSource
                                          {
                                              RepoUrl = new Uri("https://github.com/org/repo"),
                                              Path = "",
                                              TargetRevision = ArgoCDBranchFriendlyName,
                                              Helm = new HelmConfig
                                              {
                                                  ValueFiles = new List<string>()
                                                  {
                                                      "$values/otherRepoPath/values.yaml"
                                                  }
                                              },
                                              Name = "helm-source",
                                          }, SourceTypeConstants.Helm)
                                          .WithSource(new ApplicationSource
                                          {
                                              Name = "ref-source",
                                              Ref = "values",
                                              Path = "include/",
                                              TargetRevision = ArgoCDBranchFriendlyName,
                                              RepoUrl = new Uri(OriginPath),
                                          }, SourceTypeConstants.Directory)
                                          .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCDAppWithHelmSource);
            // Act
            updater.Install(runningDeployment);

            //Assert
            const string updatedYamlContent =
                @"
image:
  repository: index.docker.io/nginx
  tag: ""1.27.1""
containerPort: 8080
service:
  type: LoadBalancer
";

            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var fileInRepo = Path.Combine(clonedRepoPath, existingYamlFile);
            fileSystem.FileExists(fileInRepo).Should().BeTrue();
            var content = fileSystem.ReadFile(fileInRepo);
            content.ReplaceLineEndings().Should().Be(updatedYamlContent.ReplaceLineEndings());

            var fileUnderPath = Path.Combine(clonedRepoPath, yamlFileUnderPath);
            fileSystem.FileExists(fileUnderPath).Should().BeTrue();
            var updatedContentUnderPath = fileSystem.ReadFile(fileUnderPath);
            updatedContentUnderPath.ReplaceLineEndings().Should().Be(contentUnderPath.ReplaceLineEndings());

            AssertOutputVariables(matchingApplicationTotalSourceCounts: "2");
        }

        [Test]
        public void UpdateImages_HelmWithoutHelmConfiguration_CommitsChangesToGitAndReturnsUpdatedImages()
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

            var existingYamlFile = "values.yaml";
            var filesInRepo = new (string, string)[]
            {
                (
                    existingYamlFile,
                    @"
image:
  repository: index.docker.io/nginx
  tag: ""1.0""
containerPort: 8080
service:
  type: LoadBalancer
"
                ),
                ("Chart.yaml", @"foo")
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            var argoCDAppWithHelmSource = new ArgoCDApplicationBuilder()
                                          .WithName("App1")
                                          .WithAnnotations(new Dictionary<string, string>()
                                          {
                                              [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                              [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                              [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                                          })
                                          .WithSource(new ApplicationSource
                                          {
                                              RepoUrl = new Uri(OriginPath),
                                              Path = "",
                                              TargetRevision = ArgoCDBranchFriendlyName
                                          },  SourceTypeConstants.Helm)
                                         
                                          .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCDAppWithHelmSource);
            // Act
            updater.Install(runningDeployment);

            //Assert
            const string updatedYamlContent =
                @"
image:
  repository: index.docker.io/nginx
  tag: ""1.27.1""
containerPort: 8080
service:
  type: LoadBalancer
";

            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var fileInRepo = Path.Combine(clonedRepoPath, existingYamlFile);
            fileSystem.FileExists(fileInRepo).Should().BeTrue();
            var content = fileSystem.ReadFile(fileInRepo);
            content.ReplaceLineEndings().Should().Be(updatedYamlContent.ReplaceLineEndings());

            AssertOutputVariables(matchingApplicationTotalSourceCounts: "1");
        }

                
        [Test]
        public void UpdateImages_HelmWithHelmConfigurationAndImplicitValuesFile_IncludesValuesFileAndCommitsChangesToGitAndReturnsUpdatedImages()
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

            var implicitValuesFile = "values.yaml";
            var explicitValuesFile = "staging-values.yaml";
            var filesInRepo = new (string, string)[]
            {
                (
                    implicitValuesFile,
                    @"
image:
  repository: index.docker.io/nginx
  tag: ""1.0""
containerPort: 8080
service:
  type: LoadBalancer
"
                ),
                (
                    explicitValuesFile,
                    @"
image:
  repository: index.docker.io/nginx
  tag: ""2.0""
containerPort: 8070
"
                ),
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            var argoCDAppWithHelmSource = new ArgoCDApplicationBuilder()
                                          .WithName("App1")
                                          .WithAnnotations(new Dictionary<string, string>()
                                          {
                                              [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                              [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                              [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                                          })
                                          .WithSource(new ApplicationSource
                                          {
                                              RepoUrl = new Uri(OriginPath),
                                              Path = "",
                                              TargetRevision = ArgoCDBranchFriendlyName,
                                              Helm = new HelmConfig()
                                              {
                                                  ValueFiles = new List<string>(){ explicitValuesFile}
                                              }
                                          },  SourceTypeConstants.Helm)
                                         
                                          .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCDAppWithHelmSource);
            // Act
            updater.Install(runningDeployment);

            //Assert
            const string updatedImplicitYamlContent =
                @"
image:
  repository: index.docker.io/nginx
  tag: ""1.27.1""
containerPort: 8080
service:
  type: LoadBalancer
";

            const string updatedExplicitYamlContent =
                @"
image:
  repository: index.docker.io/nginx
  tag: ""1.27.1""
containerPort: 8070
";

            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var implicitFileInRepo = Path.Combine(clonedRepoPath, implicitValuesFile);
            fileSystem.FileExists(implicitFileInRepo).Should().BeTrue();
            var implicitCcontent = fileSystem.ReadFile(implicitFileInRepo);
            implicitCcontent.ReplaceLineEndings().Should().Be(updatedImplicitYamlContent.ReplaceLineEndings());

            var explicitFileInRepo = Path.Combine(clonedRepoPath, explicitValuesFile);
            fileSystem.FileExists(explicitFileInRepo).Should().BeTrue();
            var explicitContent = fileSystem.ReadFile(explicitFileInRepo);
            explicitContent.ReplaceLineEndings().Should().Be(updatedExplicitYamlContent.ReplaceLineEndings());

            AssertOutputVariables(matchingApplicationTotalSourceCounts: "1");
        }

        [Test]
        public void UpdateImages_HelmWithHelmConfigurationAndNoImplicitValuesFile_ExcludesValuesFileAndCommitsChangesToGitAndReturnsUpdatedImages()
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

            var explicitValuesFile = "staging-values.yaml";
            var filesInRepo = new (string, string)[]
            {
                (
                    explicitValuesFile,
                    @"
image:
  repository: index.docker.io/nginx
  tag: ""2.0""
containerPort: 8070
"
                ),
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            var argoCDAppWithHelmSource = new ArgoCDApplicationBuilder()
                                          .WithName("App1")
                                          .WithAnnotations(new Dictionary<string, string>()
                                          {
                                              [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                              [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                              [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                                          })
                                          .WithSource(new ApplicationSource
                                          {
                                              RepoUrl = new Uri(OriginPath),
                                              Path = "",
                                              TargetRevision = ArgoCDBranchFriendlyName,
                                              Helm = new HelmConfig()
                                              {
                                                  ValueFiles = new List<string>(){ explicitValuesFile}
                                              }
                                          },  SourceTypeConstants.Helm)
                                         
                                          .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCDAppWithHelmSource);
            // Act
            updater.Install(runningDeployment);

            //Assert
            const string updatedExplicitYamlContent =
                @"
image:
  repository: index.docker.io/nginx
  tag: ""1.27.1""
containerPort: 8070
";

            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var explicitFileInRepo = Path.Combine(clonedRepoPath, explicitValuesFile);
            fileSystem.FileExists(explicitFileInRepo).Should().BeTrue();
            var explicitContent = fileSystem.ReadFile(explicitFileInRepo);
            explicitContent.ReplaceLineEndings().Should().Be(updatedExplicitYamlContent.ReplaceLineEndings());

            AssertOutputVariables(matchingApplicationTotalSourceCounts: "1");
        }

        [Test]
        public void UpdateImages_HelmWithHelmConfigurationIncludesImplicitValuesFile_IncludesValuesFileAndCommitsChangesToGitAndReturnsUpdatedImages()
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

            var implicitValuesFile = "values.yaml";
            var explicitValuesFile = "staging-values.yaml";
            var filesInRepo = new (string, string)[]
            {
                (
                    implicitValuesFile,
                    @"
image:
  repository: index.docker.io/nginx
  tag: ""1.0""
containerPort: 8080
service:
  type: LoadBalancer
"
                ),
                (
                    explicitValuesFile,
                    @"
image:
  repository: index.docker.io/nginx
  tag: ""2.0""
containerPort: 8070
"
                ),
            };
            originRepo.AddFilesToBranch(argoCDBranchName, filesInRepo);

            var argoCDAppWithHelmSource = new ArgoCDApplicationBuilder()
                                          .WithName("App1")
                                          .WithAnnotations(new Dictionary<string, string>()
                                          {
                                              [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                              [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                              [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                                          })
                                          .WithSource(new ApplicationSource
                                          {
                                              RepoUrl = new Uri(OriginPath),
                                              Path = "",
                                              TargetRevision = ArgoCDBranchFriendlyName,
                                              Helm = new HelmConfig()
                                              {
                                                  ValueFiles = new List<string>(){ explicitValuesFile, implicitValuesFile}
                                              }
                                          },  SourceTypeConstants.Helm)
                                         
                                          .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCDAppWithHelmSource);
            // Act
            updater.Install(runningDeployment);

            //Assert
            const string updatedImplicitYamlContent =
                @"
image:
  repository: index.docker.io/nginx
  tag: ""1.27.1""
containerPort: 8080
service:
  type: LoadBalancer
";

            const string updatedExplicitYamlContent =
                @"
image:
  repository: index.docker.io/nginx
  tag: ""1.27.1""
containerPort: 8070
";

            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var implicitFileInRepo = Path.Combine(clonedRepoPath, implicitValuesFile);
            fileSystem.FileExists(implicitFileInRepo).Should().BeTrue();
            var implicitCcontent = fileSystem.ReadFile(implicitFileInRepo);
            implicitCcontent.ReplaceLineEndings().Should().Be(updatedImplicitYamlContent.ReplaceLineEndings());

            var explicitFileInRepo = Path.Combine(clonedRepoPath, explicitValuesFile);
            fileSystem.FileExists(explicitFileInRepo).Should().BeTrue();
            var explicitContent = fileSystem.ReadFile(explicitFileInRepo);
            explicitContent.ReplaceLineEndings().Should().Be(updatedExplicitYamlContent.ReplaceLineEndings());

            AssertOutputVariables(matchingApplicationTotalSourceCounts: "1");
        }

        void AssertOutputVariables(bool updated = true, string matchingApplicationTotalSourceCounts = "1")
        {
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");
            serviceMessages.GetPropertyValue("ArgoCD.GatewayIds").Should().Be(GatewayId);
            serviceMessages.GetPropertyValue("ArgoCD.GitUris").Should().Be(updated ? new Uri(OriginPath).AbsoluteUri : string.Empty);
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedImages").Should().Be(updated ? "1" : "0");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplications").Should().Be("App1");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationTotalSourceCounts").Should().Be(matchingApplicationTotalSourceCounts);
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationMatchingSourceCounts").Should().Be("1");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplications").Should().Be(updated ? "App1" : string.Empty);
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplicationSourceCounts").Should().Be(updated ? "1" : string.Empty);
        }
    }
}

#endif