using System;
using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Commands;
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
    public class UpdateArgoCDApplicationManifestsInstallConventionTests
    {
        const string ProjectSlug = "TheProject";
        const string EnvironmentSlug = "TheEnvironment";
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        InMemoryLog log;
        string tempDirectory;
        string WorkingDirectory => Path.Combine(tempDirectory, "working");
        string PackageDirectory => Path.Combine(WorkingDirectory, UpdateArgoCDAppManifestsCommand.PackageDirectoryName);
        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser = Substitute.For<IArgoCDApplicationManifestParser>();
        readonly ICustomPropertiesLoader customPropertiesLoader = Substitute.For<ICustomPropertiesLoader>();

        string OriginPath => Path.Combine(tempDirectory, "origin");
        string RepoUrl => OriginPath;
        Repository originRepo;

        const string ArgoCDBranchFriendlyName = "devBranch";
        const string GatewayId = "Gateway1";
        readonly GitBranchName argoCDBranchName = GitBranchName.CreateFromFriendlyName(ArgoCDBranchFriendlyName);

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();
            Directory.CreateDirectory(PackageDirectory);

            originRepo = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(argoCDBranchName, OriginPath);

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
                                                                              new GitCredentialDto(new Uri(RepoUrl).AbsoluteUri, "", "")
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
                                                RepoUrl = new Uri(RepoUrl),
                                                Path = "",
                                                TargetRevision = ArgoCDBranchFriendlyName,
                                            }, SourceTypeConstants.Directory)
                                            .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);
        }

        [TearDown]
        public void Cleanup()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void ExecuteCopiesFilesOfAnyNameFromPackageIntoRepo()
        {
            const string firstFilename = "first.yaml";
            CreateFileUnderPackageDirectory(firstFilename);
            const string nestedFilename = "nested/second.yaml";
            CreateFileUnderPackageDirectory(nestedFilename);
            const string thirdFilename = "nested/third";
            CreateFileUnderPackageDirectory(thirdFilename);
            const string fourthFilename = "nested/fourth.fourth";
            CreateFileUnderPackageDirectory(fourthFilename);

            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this",
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
            };
            var allVariables = new CalamariVariables();
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;

            var convention = new UpdateArgoCDApplicationManifestsInstallConvention(fileSystem,
                                                                                   UpdateArgoCDAppManifestsCommand.PackageDirectoryName,
                                                                                   log,
                                                                                   new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                                   customPropertiesLoader,
                                                                                   argoCdApplicationManifestParser,
                                                                                   new ArgoCDManifestsFileMatcher(fileSystem),
                                                                                   Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            convention.Install(runningDeployment);

            var resultPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            var resultFirstContent = File.ReadAllText(Path.Combine(resultPath, firstFilename));
            var resultNestedContent = File.ReadAllText(Path.Combine(resultPath, nestedFilename));
            resultFirstContent.Should().Be(firstFilename);
            resultNestedContent.Should().Be(nestedFilename);
            fileSystem.FileExists(Path.Combine(resultPath, thirdFilename)).Should().BeTrue();
            fileSystem.FileExists(Path.Combine(resultPath, fourthFilename)).Should().BeTrue();

            AssertOutputVariables();
        }

        [Test]
        public void EnsureOutputDirectoryIsPurgedWhenVariableIsSetRecursiveDeletion()
        {
            // Arrange
            const string firstFilename = "first.yaml";
            CreateFileUnderPackageDirectory(firstFilename);

            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this",
                [SpecialVariables.Git.PurgeOutput] = "True",
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
            };

            //add arbitrary file to the origin repo
            var fileToPurge = "subDirectory/removeThis.yaml";
            originRepo.AddFilesToBranch(argoCDBranchName, (fileToPurge, "This file to be removed"));

            var allVariables = new CalamariVariables();
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;

            // Act
            var convention = new UpdateArgoCDApplicationManifestsInstallConvention(fileSystem,
                                                                                   UpdateArgoCDAppManifestsCommand.PackageDirectoryName,
                                                                                   log,
                                                                                   new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                                   customPropertiesLoader,
                                                                                   argoCdApplicationManifestParser,
                                                                                   new ArgoCDManifestsFileMatcher(fileSystem),
                                                                                   Substitute.For<IGitVendorAgnosticApiAdapterFactory>());
            convention.Install(runningDeployment);

            // Assert
            var resultPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            File.Exists(Path.Combine(resultPath, firstFilename)).Should().BeTrue();
            File.Exists(Path.Combine(resultPath, fileToPurge)).Should().BeFalse();

            AssertOutputVariables();
        }

        [Test]
        public void CanTemplateFilesIntoAHelmSource()
        {
            const string firstFilename = "first.yaml";
            CreateFileUnderPackageDirectory(firstFilename);

            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this",
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
            };
            var allVariables = new CalamariVariables();
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;

            var argoCDAppWithHelmSource = new ArgoCDApplicationBuilder()
                                          .WithName("App1")
                                          .WithAnnotations(new Dictionary<string, string>()
                                          {
                                              [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                              [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                          })
                                          .WithSource(new ApplicationSource()
                                          {
                                              RepoUrl = new Uri(RepoUrl),
                                              Path = "",
                                              TargetRevision = ArgoCDBranchFriendlyName,
                                              Helm = new HelmConfig()
                                              {
                                                  ValueFiles = new List<string>()
                                                  {
                                                      "subpath/values1.yaml",
                                                      "otherPath/values2.yaml",
                                                      "$ref/otherRepoPath/values.yaml"
                                                  }
                                              }
                                          }, SourceTypeConstants.Helm)
                                          .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCDAppWithHelmSource);

            var convention = new UpdateArgoCDApplicationManifestsInstallConvention(fileSystem,
                                                                                   UpdateArgoCDAppManifestsCommand.PackageDirectoryName,
                                                                                   log,
                                                                                   new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                                   customPropertiesLoader,
                                                                                   argoCdApplicationManifestParser,
                                                                                   new ArgoCDManifestsFileMatcher(fileSystem),
                                                                                   Substitute.For<IGitVendorAgnosticApiAdapterFactory>());

            convention.Install(runningDeployment);

            // Assert
            var resultPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            File.Exists(Path.Combine(resultPath, firstFilename)).Should().BeTrue();

            AssertOutputVariables();
        }

        [Test]
        public void CanUpdateReferenceSource()
        {
            const string firstFilename = "first.yaml";
            CreateFileUnderPackageDirectory(firstFilename);

            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this",
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
            };
            var allVariables = new CalamariVariables();
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;

            var argoCDAppWithHelmSource = new ArgoCDApplicationBuilder()
                                          .WithName("App1")
                                          .WithAnnotations(new Dictionary<string, string>()
                                          {
                                              [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("refSourceName"))] = ProjectSlug,
                                              [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("refSourceName"))] = EnvironmentSlug,
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
                                                      "subpath/values1.yaml",
                                                      "otherPath/values2.yaml",
                                                      "$values/otherRepoPath/values.yaml"
                                                  }
                                              },
                                          }, SourceTypeConstants.Helm)
                                          .WithSource(new ApplicationSource
                                          {
                                              Name = "refSourceName",
                                              Ref = "values",
                                              TargetRevision = ArgoCDBranchFriendlyName,
                                              RepoUrl = new Uri(RepoUrl),
                                          }, SourceTypeConstants.Directory)
                                          .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCDAppWithHelmSource);

            var convention = new UpdateArgoCDApplicationManifestsInstallConvention(fileSystem,
                                                                                   UpdateArgoCDAppManifestsCommand.PackageDirectoryName,
                                                                                   log,
                                                                                   new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                                   customPropertiesLoader,
                                                                                   argoCdApplicationManifestParser,
                                                                                   new ArgoCDManifestsFileMatcher(fileSystem),
                                                                                   Substitute.For<IGitVendorAgnosticApiAdapterFactory>());

            convention.Install(runningDeployment);

            // Assert
            var resultPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            File.Exists(Path.Combine(resultPath, firstFilename)).Should().BeTrue();

            AssertOutputVariables(matchingApplicationTotalSourceCounts: "2");
        }

        [Test]
        public void WillNotUpdateAReferenceSourceWhichHasAPath()
        {
            const string firstFilename = "first.yaml";
            CreateFileUnderPackageDirectory(firstFilename);

            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this",
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
            };
            var allVariables = new CalamariVariables();
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;

            var argoCDAppWithHelmSource = new ArgoCDApplicationBuilder()
                                          .WithName("App1")
                                          .WithAnnotations(new Dictionary<string, string>()
                                          {
                                              [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("refSourceName"))] = ProjectSlug,
                                              [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("refSourceName"))] = EnvironmentSlug,
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
                                                      "subpath/values1.yaml",
                                                      "otherPath/values2.yaml",
                                                      "$ref/otherRepoPath/values.yaml"
                                                  }
                                              }
                                          }, SourceTypeConstants.Helm)
                                          .WithSource(new ApplicationSource
                                                      {
                                                          Name = "refSourceName",
                                                          Ref = "valuesFiles",
                                                          Path = "otherPath/values1.yaml", //this should cause an error
                                                          TargetRevision = ArgoCDBranchFriendlyName,
                                                          RepoUrl = new Uri(RepoUrl),
                                                      },
                                                      SourceTypeConstants.Directory)
                                          .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCDAppWithHelmSource);

            var convention = new UpdateArgoCDApplicationManifestsInstallConvention(fileSystem,
                                                                                   UpdateArgoCDAppManifestsCommand.PackageDirectoryName,
                                                                                   log,
                                                                                   new DeploymentConfigFactory(nonSensitiveCalamariVariables),
                                                                                   customPropertiesLoader,
                                                                                   argoCdApplicationManifestParser,
                                                                                   new ArgoCDManifestsFileMatcher(fileSystem),
                                                                                   Substitute.For<IGitVendorAgnosticApiAdapterFactory>());

            convention.Install(runningDeployment);

            //Assert
            var resultPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            File.Exists(Path.Combine(resultPath, firstFilename)).Should().BeFalse();

            AssertOutputVariables(false, matchingApplicationTotalSourceCounts: "2");
        }

        void AssertOutputVariables(bool updated = true, string matchingApplicationTotalSourceCounts = "1")
        {
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");
            serviceMessages.GetPropertyValue("ArgoCD.GatewayIds").Should().Be(GatewayId);
            serviceMessages.GetPropertyValue("ArgoCD.GitUris").Should().Be(updated ? new Uri(RepoUrl).AbsoluteUri : string.Empty);
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplications").Should().Be("App1");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationTotalSourceCounts").Should().Be(matchingApplicationTotalSourceCounts);
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationMatchingSourceCounts").Should().Be("1");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplications").Should().Be(updated ? "App1" : string.Empty);
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplicationSourceCounts").Should().Be(updated ? "1" : string.Empty);
        }

        //Accepts a relative path and creates a file under the package directory, which
        //contains the relative filename as its contents.
        void CreateFileUnderPackageDirectory(string filename)
        {
            var packageFile = Path.Combine(PackageDirectory, filename);
            var directory = Path.GetDirectoryName(packageFile);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(packageFile, filename);
        }
    }
}
