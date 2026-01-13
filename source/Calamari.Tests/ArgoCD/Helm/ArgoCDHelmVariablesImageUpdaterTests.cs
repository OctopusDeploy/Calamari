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
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.ArgoCD.Commands.Conventions;
using Calamari.Tests.ArgoCD.Git;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using FluentAssertions.Execution;
using LibGit2Sharp;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Helm
{
// This class is REALLY the helm side of the InstallConventionTest
    public class ArgoCDHelmVariablesImageUpdaterTests
    {
        const string ProjectSlug = "TheProject";
        const string EnvironmentSlug = "TheEnvironment";

        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        InMemoryLog log;
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        Repository originRepo;
        const string ArgoCDBranchFriendlyName = "devBranch";
        readonly GitBranchName argoCDBranchName = GitBranchName.CreateFromFriendlyName(ArgoCDBranchFriendlyName);
        NonSensitiveCalamariVariables nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables();

        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser = Substitute.For<IArgoCDApplicationManifestParser>();
        readonly ICustomPropertiesLoader customPropertiesLoader = Substitute.For<ICustomPropertiesLoader>();

        Application argoCdApplicationFromYaml;

        const string DefaultValuesFile = @"
image:
  name: nginx:1.18
";

        const string GatewayId = "Gateway1";

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
                    Name = "App1",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>()
                    {
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            Path = "files",
                            TargetRevision = argoCDBranchName.Value,
                            Helm = new HelmConfig()
                            {
                                ValueFiles = new List<string>() { "values.yml" }
                            },
                        }
                    }
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm })
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);
        }

        [Test]
        public void UpdateImages_WithNoImages_ReturnsResultWithEmptyImagesList()
        {
            // Arrange
            argoCdApplicationFromYaml.Metadata.Annotations[ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.name }}";

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,

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
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.Should().Be(DefaultValuesFile);
            
            AssertOutputVariables(false);
        }

        [Test]
        public void UpdateImages_WithNoAnnotations_ReturnsResultWithEmptyImagesList()
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
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
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
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.Should().Be(DefaultValuesFile);
            
            AssertOutputVariables(false);
        }

        [Test]
        public void UpdateImages_WithAMatchingUpdate_ReturnsResultWithImageUpdated()
        {
            // Arrange
            argoCdApplicationFromYaml.Metadata.Annotations[ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.name }}";

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
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
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.Should().Contain("nginx:1.27.1");
            
            AssertOutputVariables();
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

            argoCdApplicationFromYaml.Metadata.Annotations[ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image1.name }}, {{ .Values.image2.name }}:{{ .Values.image2.tag }}";

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
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
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.ReplaceLineEndings()
                             .Should()
                             .Be(@"
image1:
   name: nginx:1.27.1
image2:
   name: alpine
   tag: 2.2
".ReplaceLineEndings());
            
            AssertOutputVariables(updatedImages: 2);
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
                    Name = "App1",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>()
                    {
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            Path = "files",
                            TargetRevision = argoCDBranchName.Value,
                        }
                    }
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm })
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
                [PackageVariables.IndexedImage("argocd-e2e-container")] = "quay.io/argoprojlabs/argocd-e2e-container:0.3",
                [PackageVariables.IndexedPackagePurpose("argocd-e2e-container")] = "DockerImageReference",
            };

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);
            //Assert
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings()
                             .Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.3
  pullPolicy: IfNotPresent
".ReplaceLineEndings());
            
            AssertOutputVariables();
        }

        [Test]
        public void HandleHelmWithRefSource()
        {
            //Arrange
            const string valuesFile = @"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
";

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yaml", valuesFile));
            originRepo.AddFilesToBranch(argoCDBranchName, ("files/Chart.yaml", "Content Is Arbitrary"));

            argoCdApplicationFromYaml = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "App1",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("helm-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("ref-source".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("ref-source".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>()
                    {
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri("https://github.com/doesnt/exist.git"),
                            Path = "files",
                            Helm = new HelmConfig()
                            {
                                ValueFiles = new List<string>()
                                {
                                    "$values/files/values.yaml"
                                }
                            },
                            Name = "helm-source",
                        },
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Ref = "values",
                            Name = "ref-source",
                        }
                    }
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm, SourceTypeConstants.Directory })
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
                [PackageVariables.IndexedImage("argocd-e2e-container")] = "quay.io/argoprojlabs/argocd-e2e-container:0.3",
                [PackageVariables.IndexedPackagePurpose("argocd-e2e-container")] = "DockerImageReference",
            };

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);
            //Assert
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings()
                             .Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.3
  pullPolicy: IfNotPresent
".ReplaceLineEndings());
            
            AssertOutputVariables(matchingApplicationTotalSourceCounts: "2");
        }

        [Test]
        public void DontUpdateSingleSourceWithWrongScoping()
        {
            //Arrange
            const string valuesFile = @"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
";

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yaml", valuesFile));
            originRepo.AddFilesToBranch(argoCDBranchName, ("files/Chart.yaml", "Content Is Arbitrary"));

            argoCdApplicationFromYaml = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "App1",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>()
                    {
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            Path = "files",
                            TargetRevision = argoCDBranchName.Value,
                            Name = "wrong-scoping",
                        }
                    }
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Directory })
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
                [PackageVariables.IndexedImage("argocd-e2e-container")] = "quay.io/argoprojlabs/argocd-e2e-container:0.3",
                [PackageVariables.IndexedPackagePurpose("argocd-e2e-container")] = "DockerImageReference",
            };

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);
            //Assert
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings()
                             .Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
".ReplaceLineEndings());
            
            AssertOutputVariables(false, matchingApplicationMatchingSourceCounts: "0");
        }

        [Test]
        public void DontUpdateHelmRefSourceWithWrongScoping()
        {
            //Arrange
            const string valuesFile = @"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
";

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yaml", valuesFile));
            originRepo.AddFilesToBranch(argoCDBranchName, ("files/Chart.yaml", "Content Is Arbitrary"));

            argoCdApplicationFromYaml = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "App1",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("helm-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("ref-source".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("ref-source".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>()
                    {
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri("https://github.com/doesnt/exist.git"),
                            Path = "files",
                            Helm = new HelmConfig()
                            {
                                ValueFiles = new List<string>()
                                {
                                    "$values/files/values.yaml"
                                }
                            },
                            Name = "helm-source",
                        },
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Ref = "values",
                        }
                    }
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm, SourceTypeConstants.Directory })
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
                [PackageVariables.IndexedImage("argocd-e2e-container")] = "quay.io/argoprojlabs/argocd-e2e-container:0.3",
                [PackageVariables.IndexedPackagePurpose("argocd-e2e-container")] = "DockerImageReference",
            };

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);
            //Assert
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings()
                             .Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
".ReplaceLineEndings());
            
            AssertOutputVariables(false, matchingApplicationTotalSourceCounts: "2", matchingApplicationMatchingSourceCounts: "0");
        }

        [Test]
        public void WarnButDontUpdateHelmSourceWithExplicitValuesFileButMissingImagePath()
        {
            //Arrange
            const string valuesFile = @"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
";

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yaml", valuesFile));
            originRepo.AddFilesToBranch(argoCDBranchName, ("files/Chart.yaml", "Content Is Arbitrary"));

            argoCdApplicationFromYaml = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "App1",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("blah-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("helm-source".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("helm-source".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>()
                    {
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Path = "files",
                            Helm = new HelmConfig()
                            {
                                ValueFiles = new List<string>()
                                {
                                    "files/values.yaml"
                                }
                            },
                            Name = "helm-source",
                        }
                    }
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm })
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
                [PackageVariables.IndexedImage("argocd-e2e-container")] = "quay.io/argoprojlabs/argocd-e2e-container:0.3",
                [PackageVariables.IndexedPackagePurpose("argocd-e2e-container")] = "DockerImageReference",
            };

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);
            //Assert
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings()
                             .Should()
                             .Be(valuesFile.ReplaceLineEndings());

            log.MessagesWarnFormatted.Should().Contain($"The Helm source '{new Uri(OriginPath)}' is missing an annotation for the image replace path. It will not be updated.");
            
            AssertOutputVariables(false, matchingApplicationTotalSourceCounts: "1");
        }

                [Test]
        public void WarnButDontUpdateHelmSourceWithImplicitValuesFileButMissingImagePath()
        {
            //Arrange
            const string valuesFile = @"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
";

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yaml", valuesFile));
            originRepo.AddFilesToBranch(argoCDBranchName, ("files/Chart.yaml", "Content Is Arbitrary"));

            argoCdApplicationFromYaml = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "App1",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("blah-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("helm-source".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("helm-source".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>()
                    {
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Path = "files",
                            Name = "helm-source",
                        }
                    }
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm })
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
                [PackageVariables.IndexedImage("argocd-e2e-container")] = "quay.io/argoprojlabs/argocd-e2e-container:0.3",
                [PackageVariables.IndexedPackagePurpose("argocd-e2e-container")] = "DockerImageReference",
            };

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);
            //Assert
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings()
                             .Should()
                             .Be(valuesFile.ReplaceLineEndings());

            log.MessagesWarnFormatted.Should().Contain($"The Helm source '{new Uri(OriginPath)}' is missing an annotation for the image replace path. It will not be updated.");
            
            AssertOutputVariables(false, matchingApplicationTotalSourceCounts: "1");
        }

        [Test]
        public void WarnButDontUpdateHelmRefSourceWithMissingImagePath()
        {
            //Arrange
            const string valuesFile = @"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
";

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yaml", valuesFile));
            originRepo.AddFilesToBranch(argoCDBranchName, ("files/Chart.yaml", "Content Is Arbitrary"));

            argoCdApplicationFromYaml = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "App1",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("helm-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("ref-source".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("ref-source".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>()
                    {
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri("https://github.com/doesnt/exist.git"),
                            Path = "files",
                            Helm = new HelmConfig()
                            {
                                ValueFiles = new List<string>()
                                {
                                    "$values/files/values.yaml"
                                }
                            },
                        },
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Ref = "values",
                            Name = "ref-source",
                        }
                    }
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm, SourceTypeConstants.Directory })
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
                [PackageVariables.IndexedImage("argocd-e2e-container")] = "quay.io/argoprojlabs/argocd-e2e-container:0.3",
                [PackageVariables.IndexedPackagePurpose("argocd-e2e-container")] = "DockerImageReference",
            };

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);
            //Assert
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings()
                             .Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
".ReplaceLineEndings());

            log.MessagesWarnFormatted.Should().Contain("The Helm source 'https://github.com/doesnt/exist.git' is missing an annotation for the image replace path. It will not be updated.");
            
            AssertOutputVariables(false, matchingApplicationTotalSourceCounts: "2");
        }

        [Test]
        public void DontWarnHelmRefSourceWithMissingImagePathIfRefSourceNotInScope()
        {
            //Arrange
            const string valuesFile = @"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
";

            originRepo.AddFilesToBranch(argoCDBranchName, ("files/values.yaml", valuesFile));
            originRepo.AddFilesToBranch(argoCDBranchName, ("files/Chart.yaml", "Content Is Arbitrary"));

            argoCdApplicationFromYaml = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "App1",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("helm-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("dont-exist".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("dont-exist".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>()
                    {
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri("https://github.com/doesnt/exist.git"),
                            Path = "files",
                            Helm = new HelmConfig()
                            {
                                ValueFiles = new List<string>()
                                {
                                    "$values/files/values.yaml"
                                }
                            },
                        },
                        new ApplicationSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Ref = "values",
                        }
                    }
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm, SourceTypeConstants.Directory })
                }
            };
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);

            var updater = new UpdateArgoCDAppImagesInstallConvention(log,
                                                                     fileSystem,
                                                                     new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                     new CommitMessageGenerator(),
                                                                     customPropertiesLoader,
                                                                     argoCdApplicationManifestParser,
                                                                     Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
                [PackageVariables.IndexedImage("argocd-e2e-container")] = "quay.io/argoprojlabs/argocd-e2e-container:0.3",
                [PackageVariables.IndexedPackagePurpose("argocd-e2e-container")] = "DockerImageReference",
            };

            //Act
            var runningDeployment = new RunningDeployment(null, variables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = tempDirectory;

            updater.Install(runningDeployment);
            //Assert
            var resultRepo = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings()
                             .Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
".ReplaceLineEndings());

            log.MessagesWarnFormatted.Should().NotContain("The Helm source 'https://github.com/doesnt/exist.git' is missing an annotation for the image replace path. It will not be updated.");

            AssertOutputVariables(false, matchingApplicationTotalSourceCounts: "2", matchingApplicationMatchingSourceCounts: "0");
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

        void AssertOutputVariables(bool updated = true, string matchingApplicationTotalSourceCounts = "1", string matchingApplicationMatchingSourceCounts = "1", int updatedImages = 1)
        {
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");
            serviceMessages.GetPropertyValue("ArgoCD.GatewayIds").Should().Be(GatewayId);
            serviceMessages.GetPropertyValue("ArgoCD.GitUris").Should().Be(updated ? new Uri(OriginPath).AbsoluteUri : string.Empty);
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedImages").Should().Be(updated ? updatedImages.ToString() : "0");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplications").Should().Be("App1");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationTotalSourceCounts").Should().Be(matchingApplicationTotalSourceCounts);
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationMatchingSourceCounts").Should().Be(matchingApplicationMatchingSourceCounts);
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplications").Should().Be(updated ? "App1" : string.Empty);
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplicationSourceCounts").Should().Be(updated ? "1" : string.Empty);
        }
    }
}

#endif