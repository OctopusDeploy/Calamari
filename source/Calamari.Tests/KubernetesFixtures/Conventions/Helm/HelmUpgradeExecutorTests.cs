using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Conventions.Helm;
using Calamari.Kubernetes.Helm;
using Calamari.Kubernetes.Integration;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.KubernetesFixtures.Builders;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Conventions.Helm
{
    [TestFixture]
    public class HelmUpgradeExecutorTests
    {
        const string ReleaseName = "my-release";
        const int RevisionNumber = 5;

        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        ICommandLineRunner commandLineRunner;

        InMemoryLog log;
        string tempDirectory;
        CancellationTokenSource installCompletedCts;
        CancellationTokenSource installErrorCts;

        string ChartDirectory => Path.Combine(tempDirectory, "chart");

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            commandLineRunner = Substitute.For<ICommandLineRunner>();
            tempDirectory = fileSystem.CreateTemporaryDirectory();
            installCompletedCts = new CancellationTokenSource();
            installErrorCts = new CancellationTokenSource();

            SetupChartDirectory();
            SetupHelmVersionMock();
        }

        [TearDown]
        public void TearDown()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
            commandLineRunner.ClearSubstitute();
            installCompletedCts.Dispose();
            installErrorCts.Dispose();
        }

        [Test]
        public void SetsAppliedResourcesOutputVariable_WhenFeatureToggleIsEnabled()
        {
            // Arrange
            var variables = CreateVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.ArgoRolloutsSupportFeatureToggle);

            SetupHelmUpgradeMock();
            SetupHelmGetManifestMock(GetSampleManifest());

            var deployment = CreateRunningDeployment(variables);
            var executor = CreateExecutor(deployment);

            // Act
            executor.ExecuteHelmUpgrade(deployment, ReleaseName, RevisionNumber, installCompletedCts, installErrorCts);

            // Assert
            var appliedResourcesJson = variables.Get("AppliedResources");
            appliedResourcesJson.Should().NotBeNullOrEmpty();

            var deserializedResources = JsonConvert.DeserializeAnonymousType(appliedResourcesJson, new[]
            {
                new { Group = "", Version = "", Kind = "", Name = "", Namespace = "" }
            });
            deserializedResources.Should().HaveCount(2);
            deserializedResources.Should().Contain(r => r.Kind == "Deployment" && r.Name == "my-app");
            deserializedResources.Should().Contain(r => r.Kind == "Service" && r.Name == "my-app-service");
        }

        [Test]
        public void DoesNotSetAppliedResourcesOutputVariable_WhenFeatureToggleIsDisabled()
        {
            // Arrange
            var variables = CreateVariables();

            SetupHelmUpgradeMock();
            SetupHelmGetManifestMock(GetSampleManifest());

            var deployment = CreateRunningDeployment(variables);
            var executor = CreateExecutor(deployment);

            // Act
            executor.ExecuteHelmUpgrade(deployment, ReleaseName, RevisionNumber, installCompletedCts, installErrorCts);

            // Assert
            var appliedResourcesJson = variables.Get("AppliedResources");
            appliedResourcesJson.Should().BeNull();
            
            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("get manifest")));
        }

        [Test]
        public void DoesNotCallGetManifest_WhenFeatureToggleIsDisabled()
        {
            // Arrange
            var variables = CreateVariables();

            SetupHelmUpgradeMock();

            var deployment = CreateRunningDeployment(variables);
            var executor = CreateExecutor(deployment);

            // Act
            executor.ExecuteHelmUpgrade(deployment, ReleaseName, RevisionNumber, installCompletedCts, installErrorCts);

            // Assert
            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("get manifest")));
        }

        [Test]
        public void CallsGetManifest_WhenFeatureToggleIsEnabled()
        {
            // Arrange
            var variables = CreateVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.ArgoRolloutsSupportFeatureToggle);

            SetupHelmUpgradeMock();
            SetupHelmGetManifestMock(GetSampleManifest());

            var deployment = CreateRunningDeployment(variables);
            var executor = CreateExecutor(deployment);

            // Act
            executor.ExecuteHelmUpgrade(deployment, ReleaseName, RevisionNumber, installCompletedCts, installErrorCts);

            // Assert
            commandLineRunner.Received().Execute(Arg.Is<CommandLineInvocation>(i => 
                i.Arguments.Contains("get") && 
                i.Arguments.Contains("manifest") && 
                i.Arguments.Contains(ReleaseName) &&
                i.Arguments.Contains($"--revision {RevisionNumber}")));
        }

        [Test]
        public void LogsWarningAndContinues_WhenGetManifestFails()
        {
            // Arrange
            var variables = CreateVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.ArgoRolloutsSupportFeatureToggle);

            SetupHelmUpgradeMock();
            SetupHelmGetManifestMockToFail();

            var deployment = CreateRunningDeployment(variables);
            var executor = CreateExecutor(deployment);

            // Act - should not throw
            executor.ExecuteHelmUpgrade(deployment, ReleaseName, RevisionNumber, installCompletedCts, installErrorCts);

            // Assert
            log.MessagesWarnFormatted.Should().Contain(msg => msg.Contains("Failed to set applied resources output variable"));
            installCompletedCts.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public void SkipsAppliedResourcesVariable_WhenManifestIsEmpty()
        {
            // Arrange
            var variables = CreateVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.ArgoRolloutsSupportFeatureToggle);

            SetupHelmUpgradeMock();
            SetupHelmGetManifestMock(string.Empty);

            var deployment = CreateRunningDeployment(variables);
            var executor = CreateExecutor(deployment);

            // Act
            executor.ExecuteHelmUpgrade(deployment, ReleaseName, RevisionNumber, installCompletedCts, installErrorCts);

            // Assert
            var appliedResourcesJson = variables.Get("AppliedResources");
            appliedResourcesJson.Should().BeNull();
            log.MessagesVerboseFormatted.Should().Contain(msg => msg.Contains("empty, skipping applied resources"));
        }

        [Test]
        public void CancelsInstallCompletedToken_OnSuccessfulUpgrade()
        {
            // Arrange
            var variables = CreateVariables();
            SetupHelmUpgradeMock();

            var deployment = CreateRunningDeployment(variables);
            var executor = CreateExecutor(deployment);

            // Act
            executor.ExecuteHelmUpgrade(deployment, ReleaseName, RevisionNumber, installCompletedCts, installErrorCts);

            // Assert
            installCompletedCts.IsCancellationRequested.Should().BeTrue();
            installErrorCts.IsCancellationRequested.Should().BeFalse();
        }

        [Test]
        public void ThrowsCommandException_WhenUpgradeFails()
        {
            // Arrange
            var variables = CreateVariables();
            SetupHelmUpgradeMockToFail();

            var deployment = CreateRunningDeployment(variables);
            var executor = CreateExecutor(deployment);

            // Act & Assert
            var action = () => executor.ExecuteHelmUpgrade(deployment, ReleaseName, RevisionNumber, installCompletedCts, installErrorCts);
            action.Should().Throw<CommandException>().WithMessage("*non-zero exit code*");
            installErrorCts.IsCancellationRequested.Should().BeTrue();
        }

        void SetupChartDirectory()
        {
            Directory.CreateDirectory(ChartDirectory);
            File.WriteAllText(Path.Combine(ChartDirectory, "Chart.yaml"), "apiVersion: v2\nname: test-chart\nversion: 1.0.0");
        }

        void SetupHelmVersionMock()
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("version") && i.Arguments.Contains("--client")))
                             .Returns(info =>
                             {
                                 var invocation = (CommandLineInvocation)info[0];
                                 invocation.AdditionalInvocationOutputSink?.WriteInfo("v3.14.0");
                                 return new CommandResult("helm version", 0);
                             });
        }

        void SetupHelmUpgradeMock()
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("upgrade")))
                             .Returns(new CommandResult("helm upgrade", 0));
        }

        void SetupHelmUpgradeMockToFail()
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("upgrade")))
                             .Returns(new CommandResult("helm upgrade", 1));
        }

        void SetupHelmGetManifestMock(string manifestContent)
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("get") && i.Arguments.Contains("manifest")))
                             .Returns(info =>
                             {
                                 var invocation = (CommandLineInvocation)info[0];
                                 if (!string.IsNullOrEmpty(manifestContent))
                                 {
                                     invocation.AdditionalInvocationOutputSink?.WriteInfo(manifestContent);
                                 }
                                 return new CommandResult("helm get manifest", 0);
                             });
        }

        void SetupHelmGetManifestMockToFail()
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("get") && i.Arguments.Contains("manifest")))
                             .Returns(new CommandResult("helm get manifest", 1));
        }

        CalamariVariables CreateVariables()
        {
            return new CalamariVariables
            {
                [PackageVariables.Output.InstallationDirectoryPath] = ChartDirectory,
                [KnownVariables.OriginalPackageDirectoryPath] = tempDirectory
            };
        }

        RunningDeployment CreateRunningDeployment(CalamariVariables variables)
        {
            return new RunningDeployment(variables, new Dictionary<string, string>())
            {
                CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory,
                StagingDirectory = tempDirectory
            };
        }

        HelmUpgradeExecutor CreateExecutor(RunningDeployment deployment)
        {
            var helmCli = new HelmCli(log, commandLineRunner, deployment, fileSystem);
            var templateValueSourcesParser = new HelmTemplateValueSourcesParser(fileSystem, log);
            var namespaceResolver = new KubernetesManifestNamespaceResolver(
                new ApiResourcesScopeLookupBuilder().WithDefaults().Build(), 
                log);

            return new HelmUpgradeExecutor(log, fileSystem, templateValueSourcesParser, helmCli, namespaceResolver);
        }

        static string GetSampleManifest()
        {
            return @"---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-app
  namespace: default
spec:
  replicas: 1
---
apiVersion: v1
kind: Service
metadata:
  name: my-app-service
  namespace: default
spec:
  type: ClusterIP
";
        }
    }
}
