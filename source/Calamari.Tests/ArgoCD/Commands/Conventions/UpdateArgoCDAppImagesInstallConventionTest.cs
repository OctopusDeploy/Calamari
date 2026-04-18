using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Models;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Time;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Patching.JsonPatch;
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
                customPropertiesLoader,
                argoCdApplicationManifestParser,
                Substitute.For<IGitVendorPullRequestClientResolver>(),
                new SystemClock(),
                deploymentReporter,
                new ArgoCDOutputVariablesWriter(log));
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
                                                },
                                                SourceTypeConstants.Directory)
                                            .Build();

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(argoCdApplicationFromYaml);
        }

        [Test]
        public void PluginSourceType_DontUpdate()
        {
            // Arrange
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var kustomizeFile = "kustomization.yaml";
            var kustomizeFileContents = """
                                        images:
                                        - name: "docker.io/nginx"
                                          newTag: "1.25"
                                        """;
            originRepo.AddFilesToBranch(argoCDBranchName, [(kustomizeFile, kustomizeFileContents)]);

            OverrideApplicationSourceType(SourceTypeConstants.Plugin);

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
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

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
            var runningDeployment = CreateRunningDeployment(("nginx", "docker.io/nginx:1.27.1"));

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
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var yamlFilename = "include/file1.yaml";
            originRepo.AddFilesToBranch(argoCDBranchName,
            [
                (
                    yamlFilename,
                    """
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
                    """
                )
            ]);

            // Act
            updater.Install(runningDeployment);

            //Assert
            const string updatedYamlContent =
                """
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
                """;

            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            AssertFileContents(clonedRepoPath, yamlFilename, updatedYamlContent);

            using var resultRepo = new Repository(clonedRepoPath);
            resultRepo.Head.Tip.Message.TrimEnd().Should().Contain("---\nImages updated:");
            
            AssertOutputVariables();
        }

        [Test]
        public void DirectorySource_ImageMatches_Update_TargetImageInCommentDoesNotAppearInPatch()
        {
            // Arrange: the target image reference (nginx:1.27.1) appears in an inline YAML comment alongside
            // the old image field. CreateTemporaryBeforeContent uses a naive string.Replace, so the comment
            // would receive the placeholder during JSON-patch generation. The committed file must come from
            // ReplaceImages(originalContent) whose regex ignores comments — so CALAMARI_PLACEHOLDER must
            // never appear in the repo.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var yamlFilename = "include/file1.yaml";
            originRepo.AddFilesToBranch(argoCDBranchName,
            [
                (
                    yamlFilename,
                    """
                    apiVersion: apps/v1
                    kind: Deployment
                    metadata:
                      name: sample
                    spec:
                      template:
                        spec:
                          containers:
                            - name: nginx
                              image: nginx:1.19 # update to nginx:1.27.1
                    """
                )
            ]);

            // Act
            updater.Install(runningDeployment);

            // Assert: the image field is updated; the comment retains the original text; no placeholder leaks
            const string updatedYamlContent =
                """
                apiVersion: apps/v1
                kind: Deployment
                metadata:
                  name: sample
                spec:
                  template:
                    spec:
                      containers:
                        - name: nginx
                          image: nginx:1.27.1 # update to nginx:1.27.1
                """;

            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            AssertFileContents(clonedRepoPath, yamlFilename, updatedYamlContent);

            var committedContent = fileSystem.ReadFile(Path.Combine(clonedRepoPath, yamlFilename));
            committedContent.Should().NotContain("__CALAMARI_PLACEHOLDER__",
                "the internal placeholder used for JSON-patch generation must never be written to the repository");

            using var resultRepo = new Repository(clonedRepoPath);
            resultRepo.Head.Tip.Message.TrimEnd().Should().Contain("---\nImages updated:");

            AssertOutputVariables();
        }

        [Test]
        public void DirectorySource_UnknownCrd_LogsWarning()
        {
            // Arrange
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var yamlFilename = "include/file1.yaml";
            var fileContents = """
                               apiVersion: my-company.io/v1
                               kind: MyCustomApp
                               metadata:
                                 name: sample
                               spec:
                                 template:
                                   spec:
                                     containers:
                                       - name: nginx
                                         image: nginx:1.19
                               """;
            originRepo.AddFilesToBranch(argoCDBranchName, [(yamlFilename, fileContents)]);

            // Act
            updater.Install(runningDeployment);

            // Assert — file is unchanged and a warning was emitted
            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            AssertFileContents(clonedRepoPath, yamlFilename, fileContents);

            log.MessagesWarnFormatted.Should().Contain(m => m.Contains("Type 'my-company.io/v1/MyCustomApp' is not recognised by the Image Update step"));

            AssertOutputVariables(false);
        }

        [Test]
        public void DirectorySource_NoPath_DontUpdate()
        {
            // Arrange
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var yamlFilename = "include/file1.yaml";
            var fileContents = """
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
                               """;
            originRepo.AddFilesToBranch(argoCDBranchName, [(yamlFilename, fileContents)]);

            OverrideApplicationSourceType(SourceTypeConstants.Directory, path: null);

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
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var kustomizeFile = "kustomization.yaml";
            var kustomizeFileContents = """
                                        images:
                                        - name: "docker.io/nginx"
                                          newTag: "1.25"
                                        """;
            originRepo.AddFilesToBranch(argoCDBranchName, [(kustomizeFile, kustomizeFileContents)]);

            OverrideApplicationSourceType(SourceTypeConstants.Kustomize, path: null);

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
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var kustomizeFile = "kustomization.yaml";
            originRepo.AddFilesToBranch(argoCDBranchName, [(kustomizeFile,
                """
                apiVersion: kustomize.config.k8s.io/v1beta1
                kind: Kustomization
                images:
                - name: "docker.io/nginx"
                  newTag: "1.25"
                """)]);

            OverrideApplicationSourceType(SourceTypeConstants.Kustomize);

            // Act
            updater.Install(runningDeployment);

            // Assert
            var updatedYamlContent = """
                                     apiVersion: kustomize.config.k8s.io/v1beta1
                                     kind: Kustomization
                                     images:
                                     - name: "docker.io/nginx"
                                       newTag: "1.27.1"
                                     """;
            var clonedRepoPath = RepositoryHelpers.CloneOrigin(tempDirectory, OriginPath, argoCDBranchName);
            AssertFileContents(clonedRepoPath, kustomizeFile, updatedYamlContent);

            AssertOutputVariables();
        }

        [Test]
        public void KustomizeSource_NoKustomizationFile_DontUpdate()
        {
            // Arrange
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var existingYamlFile = "include/file1.yaml";
            var existingYamlContent = """
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
                                      """;
            originRepo.AddFilesToBranch(argoCDBranchName, [(existingYamlFile, existingYamlContent)]);

            OverrideApplicationSourceType(SourceTypeConstants.Kustomize);

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
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var yamlFilename = Path.Combine("include", "file1.yaml");
            originRepo.AddFilesToBranch(argoCDBranchName,
            [
                (
                    yamlFilename,
                    """
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
                    """
                )
            ]);

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.UpdatedImages.Should().BeEquivalentTo(["nginx:1.27.1"]);
            actual.GitReposUpdated.Should().HaveCount(1);
            actual.TrackedSourceDetails.Should().HaveCount(1);

            var sourceDetails = actual.TrackedSourceDetails.First();
            sourceDetails.CommitSha.Should().HaveLength(40);
            sourceDetails.ReplacedFiles.Should().BeEmpty();

            sourceDetails.PatchedFiles.Should()
                         .BeEquivalentTo([
                             new FileJsonPatch(yamlFilename, SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"))),
                         ]);
        }

        [Test]
        public void DirectorySource_ImageAlreadyAtTargetTag_TracksSourceWithEmptyCommitShaAndPatch()
        {
            // Arrange
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var yamlFilename = Path.Combine("include", "file1.yaml");
            originRepo.AddFilesToBranch(argoCDBranchName, [(yamlFilename, MakeDeploymentYaml("sample-deployment", "nginx:1.27.1"))]);

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeFalse("image is already at the target tag so no commit should be made");
            actual.UpdatedImages.Should().BeEmpty();
            actual.TrackedSourceDetails.Should().HaveCount(1, "source should still be tracked for the no-op case");

            var sourceDetails = actual.TrackedSourceDetails.First();
            sourceDetails.CommitSha.Should().BeNull("no commit was made");
            sourceDetails.PatchedFiles.Should()
                         .BeEquivalentTo([
                             new FileJsonPatch(yamlFilename, SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"))),
                         ]);
        }

        [TestCase(false, TestName = "DirectorySource_SameImage_OneOutdated_OneUpToDate_SameFile")]
        [TestCase(true, TestName = "DirectorySource_SameImage_OneOutdated_OneUpToDate_SeparateFiles")]
        public void DirectorySource_SameImage_OneOutdated_OneUpToDate(bool useSeparateFiles)
        {
            // Arrange
            // One nginx reference is outdated (nginx:1.19), the other is already at the target (nginx:1.27.1).
            // Same file: one PatchedFiles entry with two replace operations.
            // Separate files: two PatchedFiles entries each with one replace operation.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var file1 = Path.Combine("include", "file1.yaml");
            var file2 = Path.Combine("include", "file2.yaml");

            if (useSeparateFiles)
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("nginx-outdated", "nginx:1.19")),
                    (file2, MakeDeploymentYaml("nginx-current", "nginx:1.27.1")),
                ]);
            }
            else
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("nginx-deployment", "nginx:1.19", "nginx:1.27.1")),
                ]);
            }

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeTrue();
            actual.UpdatedImages.Should().BeEquivalentTo(["nginx:1.27.1"]);
            actual.TrackedSourceDetails.Should().HaveCount(1);

            var sourceDetails = actual.TrackedSourceDetails.First();
            sourceDetails.CommitSha.Should().HaveLength(40);
            sourceDetails.ReplacedFiles.Should().BeEmpty();

            var singleContainerPatch = SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"));

            if (useSeparateFiles)
            {
                // Each file is processed independently — one patch entry per file, one operation each.
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, singleContainerPatch),
                                 new FileJsonPatch(file2, singleContainerPatch),
                             ]);
            }
            else
            {
                // Both containers are in one file — one patch entry with two replace operations.
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, SerializeReplacePatch(
                                     ("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"),
                                     ("/0/spec/template/spec/containers/1/image", "nginx:1.27.1"))),
                             ]);
            }
        }

        [TestCase(false, TestName = "DirectorySource_DifferentImages_OneOutdated_OneUpToDate_SameFile")]
        [TestCase(true, TestName = "DirectorySource_DifferentImages_OneOutdated_OneUpToDate_SeparateFiles")]
        public void DirectorySource_DifferentImages_OneOutdated_OneUpToDate(bool useSeparateFiles)
        {
            // Arrange
            // nginx:1.19 is outdated (target is 1.27.1); redis:7.0 is already at the target tag.
            // Same file: one PatchedFiles entry with two replace operations (one per targeted image).
            // Separate files: two PatchedFiles entries each with one replace operation.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(
                ("nginx", "index.docker.io/nginx:1.27.1"),
                ("redis", "index.docker.io/redis:7.0"));

            var file1 = Path.Combine("include", "file1.yaml");
            var file2 = Path.Combine("include", "file2.yaml");

            if (useSeparateFiles)
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("nginx-deployment", "nginx:1.19")),
                    (file2, MakeDeploymentYaml("redis-deployment", "redis:7.0")),
                ]);
            }
            else
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("sample-deployment", "nginx:1.19", "redis:7.0")),
                ]);
            }

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeTrue();
            actual.UpdatedImages.Should().BeEquivalentTo(["nginx:1.27.1"], "only nginx needed updating");
            actual.TrackedSourceDetails.Should().HaveCount(1);

            var sourceDetails = actual.TrackedSourceDetails.First();
            sourceDetails.CommitSha.Should().HaveLength(40);
            sourceDetails.ReplacedFiles.Should().BeEmpty();

            if (useSeparateFiles)
            {
                // Each file is processed independently — one patch entry per file, one operation each.
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"))),
                                 new FileJsonPatch(file2, SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "redis:7.0"))),
                             ]);
            }
            else
            {
                // Both containers are in one file — one patch entry with two replace operations.
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, SerializeReplacePatch(
                                     ("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"),
                                     ("/0/spec/template/spec/containers/1/image", "redis:7.0"))),
                             ]);
            }
        }

        [TestCase(false, TestName = "DirectorySource_SameImage_BothOutdated_SameFile")]
        [TestCase(true, TestName = "DirectorySource_SameImage_BothOutdated_SeparateFiles")]
        public void DirectorySource_SameImage_BothOutdated(bool useSeparateFiles)
        {
            // Arrange
            // Two containers both reference nginx but at different outdated tags.
            // Both should be updated and appear in the patch.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var file1 = Path.Combine("include", "file1.yaml");
            var file2 = Path.Combine("include", "file2.yaml");

            if (useSeparateFiles)
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("nginx-a", "nginx:1.19")),
                    (file2, MakeDeploymentYaml("nginx-b", "nginx:1.18")),
                ]);
            }
            else
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("nginx-deployment", "nginx:1.19", "nginx:1.18")),
                ]);
            }

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeTrue();
            actual.UpdatedImages.Should().BeEquivalentTo(["nginx:1.27.1"]);
            actual.TrackedSourceDetails.Should().HaveCount(1);

            var sourceDetails = actual.TrackedSourceDetails.First();
            sourceDetails.CommitSha.Should().HaveLength(40);
            sourceDetails.ReplacedFiles.Should().BeEmpty();

            var singleContainerPatch = SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"));

            if (useSeparateFiles)
            {
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, singleContainerPatch),
                                 new FileJsonPatch(file2, singleContainerPatch),
                             ]);
            }
            else
            {
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, SerializeReplacePatch(
                                     ("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"),
                                     ("/0/spec/template/spec/containers/1/image", "nginx:1.27.1"))),
                             ]);
            }
        }

        [TestCase(false, TestName = "DirectorySource_DifferentImages_BothOutdated_SameFile")]
        [TestCase(true, TestName = "DirectorySource_DifferentImages_BothOutdated_SeparateFiles")]
        public void DirectorySource_DifferentImages_BothOutdated(bool useSeparateFiles)
        {
            // Arrange
            // nginx:1.19 needs updating to 1.27.1; redis:6.2 needs updating to 7.0.
            // Both should be updated and appear in the patch.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(
                ("nginx", "index.docker.io/nginx:1.27.1"),
                ("redis", "index.docker.io/redis:7.0"));

            var file1 = Path.Combine("include", "file1.yaml");
            var file2 = Path.Combine("include", "file2.yaml");

            if (useSeparateFiles)
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("nginx-deployment", "nginx:1.19")),
                    (file2, MakeDeploymentYaml("redis-deployment", "redis:6.2")),
                ]);
            }
            else
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("sample-deployment", "nginx:1.19", "redis:6.2")),
                ]);
            }

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeTrue();
            actual.UpdatedImages.Should().BeEquivalentTo(["nginx:1.27.1", "redis:7.0"]);
            actual.TrackedSourceDetails.Should().HaveCount(1);

            var sourceDetails = actual.TrackedSourceDetails.First();
            sourceDetails.CommitSha.Should().HaveLength(40);
            sourceDetails.ReplacedFiles.Should().BeEmpty();

            if (useSeparateFiles)
            {
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"))),
                                 new FileJsonPatch(file2, SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "redis:7.0"))),
                             ]);
            }
            else
            {
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, SerializeReplacePatch(
                                     ("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"),
                                     ("/0/spec/template/spec/containers/1/image", "redis:7.0"))),
                             ]);
            }
        }

        [TestCase(false, TestName = "DirectorySource_SameImage_BothUpToDate_SameFile")]
        [TestCase(true, TestName = "DirectorySource_SameImage_BothUpToDate_SeparateFiles")]
        public void DirectorySource_SameImage_BothUpToDate(bool useSeparateFiles)
        {
            // Arrange
            // Two containers both reference nginx:1.27.1, already at the target.
            // AlreadyUpToDateImages = {"nginx:1.27.1"} — a single entry covering both containers.
            // CreateTemporaryBeforeContent replaces all occurrences, producing two patch operations.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var file1 = Path.Combine("include", "file1.yaml");
            var file2 = Path.Combine("include", "file2.yaml");

            if (useSeparateFiles)
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("nginx-a", "nginx:1.27.1")),
                    (file2, MakeDeploymentYaml("nginx-b", "nginx:1.27.1")),
                ]);
            }
            else
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("nginx-deployment", "nginx:1.27.1", "nginx:1.27.1")),
                ]);
            }

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeFalse();
            actual.UpdatedImages.Should().BeEmpty();
            actual.TrackedSourceDetails.Should().HaveCount(1);

            var sourceDetails = actual.TrackedSourceDetails.First();
            sourceDetails.CommitSha.Should().BeNull();
            sourceDetails.ReplacedFiles.Should().BeEmpty();

            var singleContainerPatch = SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"));

            if (useSeparateFiles)
            {
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, singleContainerPatch),
                                 new FileJsonPatch(file2, singleContainerPatch),
                             ]);
            }
            else
            {
                // AlreadyUpToDateImages = {"nginx:1.27.1"} — both occurrences get the placeholder,
                // producing two replace operations in the no-op patch.
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, SerializeReplacePatch(
                                     ("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"),
                                     ("/0/spec/template/spec/containers/1/image", "nginx:1.27.1"))),
                             ]);
            }
        }

        [TestCase(false, TestName = "DirectorySource_DifferentImages_BothUpToDate_SameFile")]
        [TestCase(true, TestName = "DirectorySource_DifferentImages_BothUpToDate_SeparateFiles")]
        public void DirectorySource_DifferentImages_BothUpToDate(bool useSeparateFiles)
        {
            // Arrange
            // nginx:1.27.1 and redis:7.0 are both already at the target tag.
            // AlreadyUpToDateImages = {"nginx:1.27.1", "redis:7.0"} — two distinct entries.
            // CreateTemporaryBeforeContent replaces each independently, producing two patch operations.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(
                ("nginx", "index.docker.io/nginx:1.27.1"),
                ("redis", "index.docker.io/redis:7.0"));

            var file1 = Path.Combine("include", "file1.yaml");
            var file2 = Path.Combine("include", "file2.yaml");

            if (useSeparateFiles)
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("nginx-deployment", "nginx:1.27.1")),
                    (file2, MakeDeploymentYaml("redis-deployment", "redis:7.0")),
                ]);
            }
            else
            {
                originRepo.AddFilesToBranch(argoCDBranchName,
                [
                    (file1, MakeDeploymentYaml("sample-deployment", "nginx:1.27.1", "redis:7.0")),
                ]);
            }

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeFalse();
            actual.UpdatedImages.Should().BeEmpty();
            actual.TrackedSourceDetails.Should().HaveCount(1);

            var sourceDetails = actual.TrackedSourceDetails.First();
            sourceDetails.CommitSha.Should().BeNull();
            sourceDetails.ReplacedFiles.Should().BeEmpty();

            if (useSeparateFiles)
            {
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"))),
                                 new FileJsonPatch(file2, SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "redis:7.0"))),
                             ]);
            }
            else
            {
                sourceDetails.PatchedFiles.Should()
                             .BeEquivalentTo([
                                 new FileJsonPatch(file1, SerializeReplacePatch(
                                     ("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"),
                                     ("/0/spec/template/spec/containers/1/image", "redis:7.0"))),
                             ]);
            }
        }

        RunningDeployment CreateRunningDeployment(params (string packageName, string imageReference)[] images)
        {
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlug,
                [DeploymentEnvironment.Slug] = EnvironmentSlug,
            };
            foreach (var (packageName, imageReference) in images)
            {
                variables[PackageVariables.IndexedImage(packageName)] = imageReference;
                variables[PackageVariables.IndexedPackagePurpose(packageName)] = "DockerImageReference";
            }
            var deployment = new RunningDeployment(null, variables);
            deployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            deployment.StagingDirectory = tempDirectory;
            return deployment;
        }

        Func<IReadOnlyList<ProcessApplicationResult>> CaptureReporterResults()
        {
            IReadOnlyList<ProcessApplicationResult> captured = null;
            deploymentReporter.ReportFilesUpdated(Arg.Any<GitCommitParameters>(), Arg.Do<IReadOnlyList<ProcessApplicationResult>>(x => captured = x));
            return () => captured;
        }

        const string NoPath = null;

        void OverrideApplicationSourceType(string sourceType, string path = "")
        {
            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(new ArgoCDApplicationBuilder()
                                                        .WithName("App1")
                                                        .WithAnnotations(new Dictionary<string, string>
                                                        {
                                                            [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlug,
                                                            [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlug,
                                                        })
                                                        .WithSource(new ApplicationSource
                                                        {
                                                            OriginalRepoUrl = OriginPath,
                                                            Path = path,
                                                            TargetRevision = ArgoCDBranchFriendlyName,
                                                        }, sourceType)
                                                        .Build());
        }

        [Test]
        public void KustomizeSource_ImageAlreadyAtTargetTag_TracksSourceWithNullCommitSha()
        {
            // Arrange
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            originRepo.AddFilesToBranch(argoCDBranchName, [("kustomization.yaml",
                """
                apiVersion: kustomize.config.k8s.io/v1beta1
                kind: Kustomization
                images:
                - name: "docker.io/nginx"
                  newTag: "1.27.1"
                """)]);

            OverrideApplicationSourceType(SourceTypeConstants.Kustomize);

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeFalse("image is already at the target tag so no commit should be made");
            actual.UpdatedImages.Should().BeEmpty();
            actual.TrackedSourceDetails.Should().HaveCount(1, "source should still be tracked for the no-op case");

            var sourceDetails = actual.TrackedSourceDetails.First();
            sourceDetails.CommitSha.Should().BeNull("no commit was made");
            sourceDetails.PatchedFiles.Should()
                         .BeEquivalentTo([
                             new FileJsonPatch("kustomization.yaml", SerializeReplacePatch(("/0/images/0/newTag", "1.27.1"))),
                         ]);
        }

        [Test]
        public void MultiSource_OneSourceUpdated_OtherAlreadyAtTarget_BothTracked()
        {
            // Arrange: Source 0 has an outdated image; Source 1 already has the target image.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var file0 = Path.Combine("source0", "deployment.yaml");
            var file1 = Path.Combine("source1", "deployment.yaml");
            originRepo.AddFilesToBranch(argoCDBranchName, [
                (file0, MakeDeploymentYaml("source0-deployment", "nginx:1.19")),
                (file1, MakeDeploymentYaml("source1-deployment", "nginx:1.27.1")),
            ]);

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(new ArgoCDApplicationBuilder()
                                                        .WithName("App1")
                                                        .WithAnnotations(new Dictionary<string, string>
                                                        {
                                                            [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("source0"))] = ProjectSlug,
                                                            [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("source0"))] = EnvironmentSlug,
                                                            [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("source1"))] = ProjectSlug,
                                                            [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("source1"))] = EnvironmentSlug,
                                                        })
                                                        .WithSource(new ApplicationSource { OriginalRepoUrl = OriginPath, Path = "source0", Name = "source0", TargetRevision = ArgoCDBranchFriendlyName }, SourceTypeConstants.Directory)
                                                        .WithSource(new ApplicationSource { OriginalRepoUrl = OriginPath, Path = "source1", Name = "source1", TargetRevision = ArgoCDBranchFriendlyName }, SourceTypeConstants.Directory)
                                                        .Build());

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeTrue("source 0 had an outdated image");
            actual.UpdatedImages.Should().BeEquivalentTo(["nginx:1.27.1"]);
            actual.TrackedSourceDetails.Should().HaveCount(2, "both sources should be tracked regardless of whether they made a commit");

            var singleContainerPatch = SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"));

            var source0 = actual.TrackedSourceDetails.First(d => d.SourceIndex == 0);
            source0.CommitSha.Should().HaveLength(40, "source 0 had an outdated image and was committed");
            source0.PatchedFiles.Should()
                   .BeEquivalentTo([
                       new FileJsonPatch(file0, singleContainerPatch),
                   ]);

            var source1 = actual.TrackedSourceDetails.First(d => d.SourceIndex == 1);
            source1.CommitSha.Should().BeNull("source 1 was already at the target tag so no commit was made");
            source1.PatchedFiles.Should()
                   .BeEquivalentTo([
                       new FileJsonPatch(file1, singleContainerPatch),
                   ]);
        }

        [Test]
        public void MultiSource_BothSourcesAlreadyAtTarget_BothTrackedWithNullCommitSha()
        {
            // Arrange: both sources already have the target image — no commits expected.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var file0 = Path.Combine("source0", "deployment.yaml");
            var file1 = Path.Combine("source1", "deployment.yaml");
            originRepo.AddFilesToBranch(argoCDBranchName, [
                (file0, MakeDeploymentYaml("source0-deployment", "nginx:1.27.1")),
                (file1, MakeDeploymentYaml("source1-deployment", "nginx:1.27.1")),
            ]);

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(new ArgoCDApplicationBuilder()
                                                        .WithName("App1")
                                                        .WithAnnotations(new Dictionary<string, string>
                                                        {
                                                            [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("source0"))] = ProjectSlug,
                                                            [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("source0"))] = EnvironmentSlug,
                                                            [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("source1"))] = ProjectSlug,
                                                            [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("source1"))] = EnvironmentSlug,
                                                        })
                                                        .WithSource(new ApplicationSource { OriginalRepoUrl = OriginPath, Path = "source0", Name = "source0", TargetRevision = ArgoCDBranchFriendlyName }, SourceTypeConstants.Directory)
                                                        .WithSource(new ApplicationSource { OriginalRepoUrl = OriginPath, Path = "source1", Name = "source1", TargetRevision = ArgoCDBranchFriendlyName }, SourceTypeConstants.Directory)
                                                        .Build());

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeFalse("neither source required a commit");
            actual.Tracked.Should().BeTrue("both sources are in scope and should be tracked");
            actual.UpdatedImages.Should().BeEmpty();
            actual.TrackedSourceDetails.Should().HaveCount(2, "both in-scope sources should be tracked regardless of commit status");

            var singleContainerPatch = SerializeReplacePatch(("/0/spec/template/spec/containers/0/image", "nginx:1.27.1"));

            var source0 = actual.TrackedSourceDetails.First(d => d.SourceIndex == 0);
            source0.CommitSha.Should().BeNull("source 0 was already at the target tag");
            source0.PatchedFiles.Should()
                   .BeEquivalentTo([
                       new FileJsonPatch(file0, singleContainerPatch),
                   ]);

            var source1 = actual.TrackedSourceDetails.First(d => d.SourceIndex == 1);
            source1.CommitSha.Should().BeNull("source 1 was already at the target tag");
            source1.PatchedFiles.Should()
                   .BeEquivalentTo([
                       new FileJsonPatch(file1, singleContainerPatch),
                   ]);
        }

        [Test]
        public void MultiSource_OutOfScopeSource_IsNotTracked()
        {
            // Arrange: source 0 has a mismatched project scope and must not be tracked.
            // Source 1 is in scope and has an outdated image that gets updated.
            var updater = CreateConvention();
            var runningDeployment = CreateRunningDeployment(("nginx", "index.docker.io/nginx:1.27.1"));

            var file0 = Path.Combine("source0", "deployment.yaml");
            var file1 = Path.Combine("source1", "deployment.yaml");
            originRepo.AddFilesToBranch(argoCDBranchName, [
                (file0, MakeDeploymentYaml("source0-deployment", "nginx:1.19")),
                (file1, MakeDeploymentYaml("source1-deployment", "nginx:1.19")),
            ]);

            argoCdApplicationManifestParser.ParseManifest(Arg.Any<string>())
                                           .Returns(new ArgoCDApplicationBuilder()
                                                        .WithName("App1")
                                                        .WithAnnotations(new Dictionary<string, string>
                                                        {
                                                            // source0 is annotated for a different project — out of scope for this deployment
                                                            [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("source0"))] = "other-project",
                                                            [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("source0"))] = EnvironmentSlug,
                                                            [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("source1"))] = ProjectSlug,
                                                            [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("source1"))] = EnvironmentSlug,
                                                        })
                                                        .WithSource(new ApplicationSource { OriginalRepoUrl = OriginPath, Path = "source0", Name = "source0", TargetRevision = ArgoCDBranchFriendlyName }, SourceTypeConstants.Directory)
                                                        .WithSource(new ApplicationSource { OriginalRepoUrl = OriginPath, Path = "source1", Name = "source1", TargetRevision = ArgoCDBranchFriendlyName }, SourceTypeConstants.Directory)
                                                        .Build());

            var getResults = CaptureReporterResults();

            // Act
            updater.Install(runningDeployment);

            // Assert
            using var scope = new AssertionScope();
            var results = getResults();
            results.Should().NotBeNull();
            var actual = results.Single();
            actual.Updated.Should().BeTrue("source 1 had an outdated image and was committed");
            actual.TrackedSourceDetails.Should().HaveCount(1, "only in-scope sources should be tracked");

            var source1 = actual.TrackedSourceDetails.Single();
            source1.SourceIndex.Should().Be(1, "source 0 was out of scope and excluded");
            source1.CommitSha.Should().HaveLength(40, "source 1 was updated and committed");
        }

        static string MakeDeploymentYaml(string name, params string[] images)
        {
            var containerName = (string image) => image.Split('/').Last().Split(':').First();
            var containers = string.Join("\n", images.Select(img =>
                $"        - name: {containerName(img)}\n          image: {img}"));
            return $"apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: {name}\nspec:\n  template:\n    spec:\n      containers:\n{containers}";
        }

        static string SerializeReplacePatch(params (string path, string value)[] operations)
            => JsonSerializer.Serialize(new JsonPatchDocument(
                operations.Select(op => JsonPatchOperation.Replace(new JsonPointer(op.path), op.value))));

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

            if (updated)
            {
                var allServiceMessages = serviceMessages.Where(sm => sm.GetValue("name")?.Contains(".CommitSha") == true).ToList();
                allServiceMessages.Should().NotBeEmpty("At least one CommitSha should be set when files are updated");
            }
        }
    }
}
