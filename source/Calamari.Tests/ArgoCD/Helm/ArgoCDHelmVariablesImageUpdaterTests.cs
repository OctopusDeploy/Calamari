#if NET
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
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

namespace Calamari.Tests.ArgoCD.Helm
{
// This class is REALLY the helm side of the InstallConventionTest
    public class ArgoCDHelmVariablesImageUpdaterTests
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        readonly InMemoryLog log = new InMemoryLog();
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        Repository originRepo;
        GitBranchName argoCDBranchName = new GitBranchName("devBranch");
        NonSensitiveCalamariVariables nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables();

        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser = Substitute.For<IArgoCDApplicationManifestParser>();
        readonly ICustomPropertiesLoader customPropertiesLoader = Substitute.For<ICustomPropertiesLoader>();

        Application argoCdApplicationFromYaml;

        const string DefaultValuesFile = @"
image:
  name: nginx:1.18
";

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
                                                                              new ArgoCDApplicationDto("Gateway1",
                                                                                                       "App1",
                                                                                                       "argocd",
                                                                                                       new[]
                                                                                                       {
                                                                                                           new ArgoCDApplicationSourceDto(OriginPath, "", argoCDBranchName.Value)
                                                                                                       },
                                                                                                       "yaml",
                                                                                                       "docker.io",
                                                                                                       null)
                                                                          },
                                                                          new GitCredentialDto[]
                                                                          {
                                                                              new GitCredentialDto(new Uri(OriginPath).AbsoluteUri, "", "")
                                                                          });
            customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>().Returns(argoCdCustomPropertiesDto);

            argoCdApplicationFromYaml = new Application()
            {
                Metadata = new Metadata()
                {
                    Namespace = "MyAppp",
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>()
                    {
                        new HelmSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            Path = "files",
                            TargetRevision = argoCDBranchName.Value,
                            Helm = new HelmConfig()
                            {
                                ValueFiles = new List<string>() { "values.yml" }
                            }
                        }
                    }
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);
        }

        [Test]
        public void UpdateImages_WithNoImages_ReturnsResultWithEmptyImagesList()
        {
            // Arrange
            argoCdApplicationFromYaml.Metadata.Annotations = new Dictionary<string, string>()
            {
                { ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey, "{{ .Values.image.name }}" }
            };

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     Substitute.For<IGitHubPullRequestCreator>(),
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser);
            var variables = new CalamariVariables
            {
                //NOTE: No Packages are defined in the variables
                // [PackageVariables.IndexedImage("nginx")] = "nginx:1.27.1",
                // [PackageVariables.IndexedPackagePurpose("nginx")] = "DockerImageReference",
            };

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yml", DefaultValuesFile));

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);

            //Assert
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.Should().Be(DefaultValuesFile);
        }

        [Test]
        public void UpdateImages_WithNoAnnotations_ReturnsResultWithEmptyImagesList()
        {
            // Arrange
            argoCdApplicationFromYaml.Metadata.Annotations = new Dictionary<string, string>();

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     Substitute.For<IGitHubPullRequestCreator>(),
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser);
            var variables = new CalamariVariables
            {
                [PackageVariables.IndexedImage("nginx")] = "nginx:1.27.1",
                [PackageVariables.IndexedPackagePurpose("nginx")] = "DockerImageReference",
            };

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yml", DefaultValuesFile));

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);

            //Assert
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.Should().Be(DefaultValuesFile);
        }

        [Test]
        public void UpdateImages_WithAMatchingUpdate_ReturnsResultWithImageUpdated()
        {
            // Arrange
            argoCdApplicationFromYaml.Metadata.Annotations = new Dictionary<string, string>()
            {
                { ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey, "{{ .Values.image.name }}" }
            };

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     Substitute.For<IGitHubPullRequestCreator>(),
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser);
            var variables = new CalamariVariables
            {
                [PackageVariables.IndexedImage("nginx")] = "nginx:1.27.1",
                [PackageVariables.IndexedPackagePurpose("nginx")] = "DockerImageReference",
            };

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yml", DefaultValuesFile));

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);

            //Assert
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.Should().Contain("nginx:1.27.1");
        }

        [Test]
        public void UpdateImages_WithMultipleMatchesInSameValuesFile_ReturnsResultWithImagesUpdated()
        {
            //Arrange
            const string multiImageValuesFile = @"
image1:
   name: nginx:1.22
image2:
   name: alpine
   tag: latest
";
            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yml", multiImageValuesFile));

            argoCdApplicationFromYaml.Metadata.Annotations = new Dictionary<string, string>()
            {
                { ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey, "{{ .Values.image1.name }}, {{ .Values.image2.name }}:{{ .Values.image2.tag }}" }
            };

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     Substitute.For<IGitHubPullRequestCreator>(),
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser);
            var variables = new CalamariVariables
            {
                [PackageVariables.IndexedImage("nginx")] = "nginx:1.27.1",
                [PackageVariables.IndexedPackagePurpose("nginx")] = "DockerImageReference",
                [PackageVariables.IndexedImage("alpine")] = "alpine:2.2",
                [PackageVariables.IndexedPackagePurpose("alpine")] = "DockerImageReference",
            };

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);

            //Assert
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.ReplaceLineEndings().Should()
                             .Be(@"
image1:
   name: nginx:1.27.1
image2:
   name: alpine
   tag: 2.2
".ReplaceLineEndings());
        }
        
        [Test]
        public void HandleAHelmChartInAnUntaggedApplication()
        {
            //Arrange
            const string multiImageValuesFile = @"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
";

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yaml", multiImageValuesFile));
            originRepo.AddFilesToBranch(argoCDBranchName, ("files/Chart.yaml", "Content Is Arbitrary"));
            
            argoCdApplicationFromYaml = new Application()
            {
                Metadata = new Metadata()
                {
                    Namespace = "MyAppp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey, "{{ .Values.image.repository }}:{{ .Values.image.tag }}" }
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>()
                    {
                        new BasicSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            Path = "files",
                            TargetRevision = argoCDBranchName.Value,
                        }
                    }
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     Substitute.For<IGitHubPullRequestCreator>(),
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser);
            var variables = new CalamariVariables
            {
                [PackageVariables.IndexedImage("argocd-e2e-container")] = "quay.io/argoprojlabs/argocd-e2e-container:0.3",
                [PackageVariables.IndexedPackagePurpose("argocd-e2e-container")] = "DockerImageReference",
            };
            
            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);
            //Assert
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings().Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.3
  pullPolicy: IfNotPresent
".ReplaceLineEndings());
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