using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands.Executors;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Commands.Executors
{
    [TestFixture]
    public class KustomizeExecutorTests
    {
        const string OverlayPath = "/kustomize-example/overlays/dev/";
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        readonly ICommandLineRunner commandLineRunner = Substitute.For<ICommandLineRunner>();
        readonly IManifestReporter manifestReporter = Substitute.For<IManifestReporter>();

        InMemoryLog log;
        List<ResourceIdentifier> receivedCallbacks;
        string tempDirectory;

        string StagingDirectory => Path.Combine(tempDirectory, "staging");

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            receivedCallbacks = new List<ResourceIdentifier>();
            tempDirectory = fileSystem.CreateTemporaryDirectory();
        }

        [TearDown]
        public void Cleanup()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
            commandLineRunner.ClearSubstitute();
            manifestReporter.ClearReceivedCalls();
        }

        [Test]
        public async Task NoOverlayPath_DoesNothing()
        {
            // Arrange
            SetupCommandLineRunnerMock();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeFalse();
            commandLineRunner.ReceivedCalls().Should().BeEmpty();
            receivedCallbacks.Should().BeEmpty();
            log.ServiceMessages.Should().BeEmpty();
            log.StandardError[0].Should().Contain("Kustomization directory not specified");
            AssertNoManifestsReported();
        }

        [Test]
        public async Task InvalidKubectlVersionJson_ReturnsFalseToIndicateFailure()
        {
            // Arrange
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(invocation => invocation.Arguments.Contains("version --client")))
                             .Returns(info =>
                                      {
                                          var invocation = (CommandLineInvocation)info[0];
                                          invocation.AdditionalInvocationOutputSink?.WriteInfo("banana");
                                          return new CommandResult("kubectl version result", 0);
                                      });
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.KustomizeOverlayPath] = OverlayPath
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeFalse();
            commandLineRunner.ReceivedCalls().Count().Should().Be(1);
            receivedCallbacks.Should().BeEmpty();
            log.ServiceMessages.Should().BeEmpty();
            log.StandardError[0].Should().Contain("Could not determine the kubectl version");
            AssertNoManifestsReported();
        }

        [Test]
        [TestCase(0, 0)]
        [TestCase(0, 24)]
        [TestCase(1, 23)]
        public async Task IncompatibleKubectlVersion_ReturnsFalseToIndicateFailure(int major, int minor)
        {
            // Arrange
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(invocation => invocation.Arguments.Contains("version --client")))
                             .Returns(info =>
                                      {
                                          var invocation = (CommandLineInvocation)info[0];
                                          invocation.AdditionalInvocationOutputSink?.WriteInfo(GetVersionJson(major, minor));
                                          return new CommandResult("kubectl version result", 0);
                                      });
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.KustomizeOverlayPath] = OverlayPath
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeFalse();
            commandLineRunner.ReceivedCalls().Count().Should().Be(1);
            receivedCallbacks.Should().BeEmpty();
            log.ServiceMessages.Should().BeEmpty();
            log.StandardError[0].Should().Contain("it needs to be v1.24");
            AssertNoManifestsReported();
        }

        [Test]
        public async Task AppliesKustomizationAndAddsResourceIdentifiers()
        {
            // Arrange
            SetupCommandLineRunnerMock();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.KustomizeOverlayPath] = OverlayPath
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeTrue();
            commandLineRunner.ReceivedCalls().Count().Should().Be(3);
            var commandLineArgs = commandLineRunner.ReceivedCalls().SelectMany(call => call.GetArguments().Select(arg => arg.ToString())).ToArray();
            commandLineArgs[0].Should().Contain("version").And.Contain("--client").And.Contain("-o json");
            commandLineArgs[1].Should().Contain("kustomize").And.Contain(OverlayPath).And.Contain("-o").And.Contain(KustomizeExecutor.HydratedKustomizeManifestFilename);
            commandLineArgs[2].Should().Contain("apply -f").And.Contain(KustomizeExecutor.HydratedKustomizeManifestFilename).And.Contain("-o json");
            receivedCallbacks.Should()
                             .BeEquivalentTo(new List<ResourceIdentifier>
                             {
                                 new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "basic-deployment", "dev"), 
                                 new ResourceIdentifier(SupportedResourceGroupVersionKinds.ServiceV1, "basic-service", "dev")
                             });
            AssertManifestsReported();
        }

        [Test]
        public async Task AppliesKustomizationWithServerSideApply()
        {
            // Arrange
            SetupCommandLineRunnerMock();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.KustomizeOverlayPath] = OverlayPath,
                [SpecialVariables.ServerSideApplyEnabled] = "true"
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeTrue();
            commandLineRunner.ReceivedCalls().Count().Should().Be(3);
            var commandLineArgs = commandLineRunner.ReceivedCalls().SelectMany(call => call.GetArguments().Select(arg => arg.ToString())).ToArray();
            commandLineArgs[1].Should().Contain("kustomize").And.Contain(OverlayPath).And.Contain("-o").And.Contain(KustomizeExecutor.HydratedKustomizeManifestFilename);
            commandLineArgs[2]
                .Should()
                .Contain("apply -f")
                .And.Contain(KustomizeExecutor.HydratedKustomizeManifestFilename)
                .And.Contain("-o json")
                .And.Contain("--server-side")
                .And.Contain("--field-manager octopus")
                .And.NotContain("--force-conflicts");
            AssertManifestsReported();
        }

        [Test]
        public async Task AppliesKustomizationWithServerSideApplyAndForceConflicts()
        {
            // Arrange
            SetupCommandLineRunnerMock();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.KustomizeOverlayPath] = OverlayPath,
                [SpecialVariables.ServerSideApplyEnabled] = "true",
                [SpecialVariables.ServerSideApplyForceConflicts] = "true"
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);
            
            var expectedKustomizeBuildArgs = $@"kustomize ""{Path.Combine(StagingDirectory, OverlayPath)}"" -o ""{Path.Combine(StagingDirectory, KustomizeExecutor.HydratedKustomizeManifestFilename)}""";
            var expectedKubectlApplyArgs = $@"apply -f ""{Path.Combine(StagingDirectory, KustomizeExecutor.HydratedKustomizeManifestFilename)}"" -o json";

            // Assert
            result.Should().BeTrue();
            commandLineRunner.ReceivedCalls().Count().Should().Be(3);
            var commandLineArgs = commandLineRunner.ReceivedCalls().SelectMany(call => call.GetArguments().Select(arg => arg.ToString())).ToArray();
            commandLineArgs[1].Should().Contain("kustomize").And.Contain(OverlayPath).And.Contain("-o").And.Contain(KustomizeExecutor.HydratedKustomizeManifestFilename);
            commandLineArgs[2]
                .Should()
                .Contain("apply -f")
                .And.Contain(KustomizeExecutor.HydratedKustomizeManifestFilename)
                .And.Contain("-o json")
                .And.Contain("--server-side")
                .And.Contain("--field-manager octopus")
                .And.Contain("--force-conflicts");
            AssertManifestsReported();
        }

        [Test]
        public async Task AppliesKustomizationAndForceConflictsIsIgnoredWithServerSideApplyDisabled()
        {
            // Arrange
            SetupCommandLineRunnerMock();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.KustomizeOverlayPath] = OverlayPath,
                [SpecialVariables.ServerSideApplyEnabled] = "false",
                [SpecialVariables.ServerSideApplyForceConflicts] = "true"
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeTrue();
            commandLineRunner.ReceivedCalls().Count().Should().Be(3);
            var commandLineArgs = commandLineRunner.ReceivedCalls().SelectMany(call => call.GetArguments().Select(arg => arg.ToString())).ToArray();
            commandLineArgs[1].Should().Contain("kustomize").And.Contain(OverlayPath).And.Contain("-o").And.Contain(KustomizeExecutor.HydratedKustomizeManifestFilename);
            commandLineArgs[2]
                .Should()
                .Contain("apply -f")
                .And.Contain(KustomizeExecutor.HydratedKustomizeManifestFilename)
                .And.Contain("-o json")
                .And.NotContain("--server-side")
                .And.NotContain("--field-manager octopus")
                .And.NotContain("--force-conflicts");
            AssertManifestsReported();
        }

        [Test]
        public async Task CommandLineReturnsNonZeroCode_ReturnsFalseToIndicateFailure()
        {
            // Arrange
            commandLineRunner.Execute(Arg.Any<CommandLineInvocation>()).Returns(new CommandResult("fail whale", 1));
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.KustomizeOverlayPath] = OverlayPath
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeFalse();
            commandLineRunner.ReceivedCalls().Should().NotBeEmpty();
            receivedCallbacks.Should().BeEmpty();
            log.ServiceMessages.Should().BeEmpty();
            AssertNoManifestsReported();
        }

        void SetupCommandLineRunnerMock()
        {
            const string resourceJson = @"{
                ""kind"": ""List"",
                ""items"": [
                    {
                        ""apiVersion"": ""v1"",
                        ""kind"": ""Service"",
                        ""metadata"": {
                            ""name"": ""basic-service"",
                            ""namespace"": ""dev""
                        }
                    },
                    {
                        ""apiVersion"": ""apps/v1"",
                        ""kind"": ""Deployment"",
                        ""metadata"": {
                            ""name"": ""basic-deployment"",
                            ""namespace"": ""dev""
                        }
                    },
                ]
            }";

            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(invocation => invocation.Arguments.Contains("kustomize")))
                             .Returns(info => new CommandResult("kubectl kustomize result", 0));

            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(invocation => invocation.Arguments.Contains("apply")))
                             .Returns(info =>
                                      {
                                          var invocation = (CommandLineInvocation)info[0];
                                          invocation.AdditionalInvocationOutputSink?.WriteInfo(resourceJson);
                                          return new CommandResult("kubectl apply result", 0);
                                      });
            
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(invocation => invocation.Arguments.Contains("version --client")))
                             .Returns(info =>
                                      {
                                          var invocation = (CommandLineInvocation)info[0];
                                          invocation.AdditionalInvocationOutputSink?.WriteInfo(GetVersionJson());
                                          return new CommandResult("kubectl version result", 0);
                                      });
        }

        static string GetVersionJson(int major = 1, int minor = 28) => $@"{{
                ""clientVersion"": {{
                    ""major"": ""{major}"",
                    ""minor"": ""{minor}"",
                    ""gitVersion"": ""v{major}.{minor}.4"",
                    ""gitCommit"": ""bae2c62678db2b5053817bc97181fcc2e8388103"",
                    ""gitTreeState"": ""clean"",
                    ""buildDate"": ""2023-11-15T16:48:52Z"",
                    ""goVersion"": ""go1.20.11"",
                    ""compiler"": ""gc"",
                    ""platform"": ""darwin/arm64""
                }}
            }}";

        IKustomizeKubernetesApplyExecutor CreateExecutor(IVariables variables)
        {
            var kubectl = new Kubectl(variables, log, commandLineRunner);
            return new KustomizeExecutor(log, kubectl, manifestReporter);
        }

        Task RecordingCallback(ResourceIdentifier[] identifiers)
        {
            receivedCallbacks.AddRange(identifiers.ToList());
            return Task.CompletedTask;
        }
        
        void AssertManifestsReported() => manifestReporter.Received().ReportManifestFileApplied(Path.Combine(StagingDirectory, KustomizeExecutor.HydratedKustomizeManifestFilename));
        
        void AssertNoManifestsReported() => manifestReporter.DidNotReceiveWithAnyArgs().ReportManifestFileApplied(default);
    }
}