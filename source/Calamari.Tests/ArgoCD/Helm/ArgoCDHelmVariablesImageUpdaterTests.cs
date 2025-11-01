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
        const string ProjectSlug = "TheProject";
        const string EnvironmentSlug = "TheEnvironment";

        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        InMemoryLog log;
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
            log = new InMemoryLog();
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
                    Name = "MyApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                    }
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
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.Should().Be(DefaultValuesFile);
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
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yml"));
            valuesFileContent.Should().Be(DefaultValuesFile);
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
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
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
                    Namespace = "MyAppp",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("helm-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("ref-source".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("ref-source".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>()
                    {
                        new HelmSource()
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
                            Name = "helm-source"
                        },
                        new ReferenceSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Ref = "values",
                            Name = "ref-source"
                        }
                    }
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
                    Namespace = "MyAppp",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
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
                            Name = "wrong-scoping"
                        }
                    }
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
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings().Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
".ReplaceLineEndings());
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
                    Namespace = "MyAppp",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("helm-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("ref-source".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("ref-source".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>()
                    {
                        new HelmSource()
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
                            Name = "helm-source"
                        },
                        new ReferenceSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Ref = "values"
                        }
                    }
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
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings().Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
".ReplaceLineEndings());
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
                    Namespace = "MyAppp",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("helm-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("ref-source".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("ref-source".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>()
                    {
                        new HelmSource()
                        {
                            RepoUrl = new Uri("https://github.com/doesnt/exist.git"),
                            Path = "files",
                            Helm = new HelmConfig()
                            {
                                ValueFiles = new List<string>()
                                {
                                    "$values/files/values.yaml"
                                }
                            }
                        },
                        new ReferenceSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Ref = "values",
                            Name = "ref-source"
                        }
                    }
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
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings().Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
".ReplaceLineEndings());

            log.MessagesWarnFormatted.Should().Contain("The Helm source 'https://github.com/doesnt/exist.git' is missing an annotation for the image replace path. It will not be updated.");
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
                    Namespace = "MyAppp",
                    Annotations = new Dictionary<string, string>()
                    {
                        [ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey("helm-source".ToApplicationSourceName())] = "{{ .Values.image.repository }}:{{ .Values.image.tag }}",
                        [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("dont-exist".ToApplicationSourceName())] = ProjectSlug,
                        [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("dont-exist".ToApplicationSourceName())] = EnvironmentSlug,
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>()
                    {
                        new HelmSource()
                        {
                            RepoUrl = new Uri("https://github.com/doesnt/exist.git"),
                            Path = "files",
                            Helm = new HelmConfig()
                            {
                                ValueFiles = new List<string>()
                                {
                                    "$values/files/values.yaml"
                                }
                            }
                        },
                        new ReferenceSource()
                        {
                            RepoUrl = new Uri(OriginPath),
                            TargetRevision = argoCDBranchName.Value,
                            Ref = "values"
                        }
                    }
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
            var resultRepo = CloneOrigin();
            var valuesFileContent = fileSystem.ReadFile(Path.Combine(resultRepo, "files", "values.yaml"));
            valuesFileContent.ReplaceLineEndings().Should()
                             .Be(@"
replicaCount: 1
image:
  repository: quay.io/argoprojlabs/argocd-e2e-container
  tag: 0.1
  pullPolicy: IfNotPresent
".ReplaceLineEndings());

            log.MessagesWarnFormatted.Should().NotContain("The Helm source 'https://github.com/doesnt/exist.git' is missing an annotation for the image replace path. It will not be updated.");
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