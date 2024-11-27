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
using Calamari.Kubernetes.Commands;
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
    public class GatherAndApplyRawYamlExecutorTests
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        readonly ICommandLineRunner commandLineRunner = Substitute.For<ICommandLineRunner>();
        readonly IManifestReporter manifestReporter = Substitute.For<IManifestReporter>();

        InMemoryLog log;
        List<ResourceIdentifier> receivedCallbacks;
        string tempDirectory;

        string StagingDirectory => Path.Combine(tempDirectory, "staging");
        string PackageDirectory => Path.Combine(tempDirectory, "staging", KubernetesDeploymentCommandBase.PackageDirectoryName);

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
        }

        [Test]
        public async Task NoYamlFileName_DoesNothing()
        {
            // Arrange
            AddTestFiles();
            SetupCommandLineRunnerMocks();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory
            };
            var runningDeployment = new RunningDeployment(variables);
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var executor = CreateExecutor(variables, fileSystem);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeTrue();
            commandLineRunner.ReceivedCalls().Should().BeEmpty();
            receivedCallbacks.Should().BeEmpty();
            log.ServiceMessages.Should().BeEmpty();
        }

        [Test]
        public async Task AppliesKubernetesManifestsAndAddsResourceIdentifiers()
        {
            // Arrange
            AddTestFiles();
            SetupCommandLineRunnerMocks();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.CustomResourceYamlFileName] = "dirA/*\ndirB/*"
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables, fileSystem);
            var expectedYamlGrouping = $"{Path.Combine(StagingDirectory, "grouped", "1")};{Path.Combine(StagingDirectory, "grouped", "2")}";

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeTrue();
            variables.Get(SpecialVariables.GroupedYamlDirectories).Should().Be(expectedYamlGrouping);

            commandLineRunner.ReceivedCalls().Count().Should().Be(2);
            var commandLineArgs = commandLineRunner.ReceivedCalls().SelectMany(call => call.GetArguments().Select(arg => arg.ToString())).ToArray();
            commandLineArgs[0].Should().Contain("apply -f").And.Contain("--recursive").And.Contain("-o json").And.Contain($"{Path.Combine("grouped", "1")}");
            commandLineArgs[1].Should().Contain("apply -f").And.Contain("--recursive").And.Contain("-o json").And.Contain($"{Path.Combine("grouped", "2")}");

            receivedCallbacks.Should()
                             .BeEquivalentTo(new List<ResourceIdentifier>
                             {
                                 new ResourceIdentifier("apps", "v1", "Deployment", "basic-deployment", "dev"), new ResourceIdentifier("", "v1", "Service", "basic-service", "dev"), new ResourceIdentifier("apps", "v1", "Deployment", "basic-deployment", "dev")
                             });
        }

        [Test]
        public async Task AppliesKubernetesManifestsWithServerSideApply()
        {
            // Arrange
            AddTestFiles();
            SetupCommandLineRunnerMocks();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.CustomResourceYamlFileName] = "dirA/*\ndirB/*",
                [SpecialVariables.ServerSideApplyEnabled] = "true"
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables, fileSystem);
            var expectedYamlGrouping = $"{Path.Combine(StagingDirectory, "grouped", "1")};{Path.Combine(StagingDirectory, "grouped", "2")}";

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeTrue();
            variables.Get(SpecialVariables.GroupedYamlDirectories).Should().Be(expectedYamlGrouping);

            commandLineRunner.ReceivedCalls().Count().Should().Be(2);
            var commandLineArgs = commandLineRunner.ReceivedCalls().SelectMany(call => call.GetArguments().Select(arg => arg.ToString())).ToArray();
            commandLineArgs[0]
                .Should()
                .Contain("apply -f")
                .And.Contain("--recursive")
                .And.Contain("-o json")
                .And.Contain($"{Path.Combine("grouped", "1")}")
                .And.Contain("--server-side")
                .And.Contain("--field-manager octopus")
                .And.NotContain("--force-conflicts");
            commandLineArgs[1]
                .Should()
                .Contain("apply -f")
                .And.Contain("--recursive")
                .And.Contain("-o json")
                .And.Contain($"{Path.Combine("grouped", "2")}")
                .And.Contain("--server-side")
                .And.Contain("--field-manager octopus")
                .And.NotContain("--force-conflicts");
        }

        [Test]
        public async Task AppliesKubernetesManifestsWithServerSideApplyAndForceConflicts()
        {
            // Arrange
            AddTestFiles();
            SetupCommandLineRunnerMocks();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.CustomResourceYamlFileName] = "dirA/*\ndirB/*",
                [SpecialVariables.ServerSideApplyEnabled] = "true",
                [SpecialVariables.ServerSideApplyForceConflicts] = "true"
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables, fileSystem);
            var expectedYamlGrouping = $"{Path.Combine(StagingDirectory, "grouped", "1")};{Path.Combine(StagingDirectory, "grouped", "2")}";

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeTrue();
            variables.Get(SpecialVariables.GroupedYamlDirectories).Should().Be(expectedYamlGrouping);

            commandLineRunner.ReceivedCalls().Count().Should().Be(2);
            var commandLineArgs = commandLineRunner.ReceivedCalls().SelectMany(call => call.GetArguments().Select(arg => arg.ToString())).ToArray();
            commandLineArgs[0]
                .Should()
                .Contain("apply -f")
                .And.Contain("--recursive")
                .And.Contain("-o json")
                .And.Contain($"{Path.Combine("grouped", "1")}")
                .And.Contain("--server-side")
                .And.Contain("--field-manager octopus")
                .And.Contain("--force-conflicts");
            commandLineArgs[1]
                .Should()
                .Contain("apply -f")
                .And.Contain("--recursive")
                .And.Contain("-o json")
                .And.Contain($"{Path.Combine("grouped", "2")}")
                .And.Contain("--server-side")
                .And.Contain("--field-manager octopus")
                .And.Contain("--force-conflicts");
        }

        [Test]
        public async Task AppliesKubernetesManifestsAndForceConflictsIsIgnoredWithServerSideApplyDisabled()
        {
            // Arrange
            AddTestFiles();
            SetupCommandLineRunnerMocks();
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.CustomResourceYamlFileName] = "dirA/*\ndirB/*",
                [SpecialVariables.ServerSideApplyEnabled] = "false",
                [SpecialVariables.ServerSideApplyForceConflicts] = "true"
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables, fileSystem);
            var expectedYamlGrouping = $"{Path.Combine(StagingDirectory, "grouped", "1")};{Path.Combine(StagingDirectory, "grouped", "2")}";

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeTrue();
            variables.Get(SpecialVariables.GroupedYamlDirectories).Should().Be(expectedYamlGrouping);

            commandLineRunner.ReceivedCalls().Count().Should().Be(2);
            var commandLineArgs = commandLineRunner.ReceivedCalls().SelectMany(call => call.GetArguments().Select(arg => arg.ToString())).ToArray();
            commandLineArgs[0]
                .Should()
                .Contain("apply -f")
                .And.Contain("--recursive")
                .And.Contain("-o json")
                .And.Contain($"{Path.Combine("grouped", "1")}")
                .And.NotContain("--server-side")
                .And.NotContain("--field-manager octopus")
                .And.NotContain("--force-conflicts");

            commandLineArgs[1]
                .Should()
                .Contain("apply -f")
                .And.Contain("--recursive")
                .And.Contain("-o json")
                .And.Contain($"{Path.Combine("grouped", "2")}")
                .And.NotContain("--server-side")
                .And.NotContain("--field-manager octopus")
                .And.NotContain("--force-conflicts");
        }

        [Test]
        public async Task CommandLineReturnsNonZeroCode_ReturnsFalseToIndicateFailure()
        {
            // Arrange
            AddTestFiles();
            commandLineRunner.Execute(Arg.Any<CommandLineInvocation>()).Returns(new CommandResult("blah", 1));
            var variables = new CalamariVariables
            {
                [KnownVariables.OriginalPackageDirectoryPath] = StagingDirectory,
                [SpecialVariables.CustomResourceYamlFileName] = "dirA/*\ndirB/*"
            };
            var runningDeployment = new RunningDeployment(variables);
            var executor = CreateExecutor(variables, fileSystem);

            // Act
            var result = await executor.Execute(runningDeployment, RecordingCallback);

            // Assert
            result.Should().BeFalse();
            commandLineRunner.ReceivedCalls().Should().NotBeEmpty();
            receivedCallbacks.Should().BeEmpty();
            log.ServiceMessages.Should().BeEmpty();
        }

        void AddTestFiles()
        {
            void CreateTemporaryTestFile(string directory)
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, Guid.NewGuid() + ".tmp");
                using (fileSystem.OpenFile(path, FileMode.OpenOrCreate, FileAccess.Read))
                {
                }
            }

            var dirA = Path.Combine(PackageDirectory, "dirA");
            var dirB = Path.Combine(PackageDirectory, "dirB");
            CreateTemporaryTestFile(dirA);
            CreateTemporaryTestFile(dirB);
            CreateTemporaryTestFile(dirB);
        }

        void SetupCommandLineRunnerMocks()
        {
            const string deploymentJson = @"{
                ""apiVersion"": ""apps/v1"",
                ""kind"": ""Deployment"",
                ""metadata"": {
                    ""name"": ""basic-deployment"",
                    ""namespace"": ""dev""
                }
            }";

            const string serviceAndDeploymentJson = @"{
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
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(invocation => invocation.Arguments.Contains(Path.Combine("grouped", "1"))))
                             .Returns(info =>
                                      {
                                          var invocation = (CommandLineInvocation)info[0];
                                          invocation.AdditionalInvocationOutputSink?.WriteInfo(deploymentJson);
                                          return new CommandResult("group 1", 0);
                                      });
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(invocation => invocation.Arguments.Contains(Path.Combine("grouped", "2"))))
                             .Returns(info =>
                                      {
                                          var invocation = (CommandLineInvocation)info[0];
                                          invocation.AdditionalInvocationOutputSink?.WriteInfo(serviceAndDeploymentJson);
                                          return new CommandResult("group 2", 0);
                                      });
        }

        IRawYamlKubernetesApplyExecutor CreateExecutor(IVariables variables, ICalamariFileSystem fs)
        {
            var kubectl = new Kubectl(variables, log, commandLineRunner);
            
            return new GatherAndApplyRawYamlExecutor(log, fs, manifestReporter, kubectl);
        }

        Task RecordingCallback(ResourceIdentifier[] identifiers)
        {
            receivedCallbacks.AddRange(identifiers.ToList());
            return Task.CompletedTask;
        }
    }
}