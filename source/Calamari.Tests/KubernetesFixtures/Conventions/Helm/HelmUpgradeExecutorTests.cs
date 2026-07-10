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
            commandLineRunner.Received().Execute(Arg.Is<CommandLineInvocation>(i => 
                i.Arguments.Contains("get") && 
                i.Arguments.Contains("manifest") && 
                i.Arguments.Contains(ReleaseName) &&
                i.Arguments.Contains($"--revision {RevisionNumber}")));

            var appliedResourcesJson = variables.Get(SpecialVariables.AppliedResources);
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
            var appliedResourcesJson = variables.Get(SpecialVariables.AppliedResources);
            appliedResourcesJson.Should().BeNull();
            
            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("get manifest")));
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
            log.MessagesWarnFormatted.Should().Contain(msg => msg.Contains($"Failed to get manifest for {ReleaseName} revision {RevisionNumber}"));
            var appliedResourcesJson = variables.Get(SpecialVariables.AppliedResources);
            appliedResourcesJson.Should().BeNull();
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
            var appliedResourcesJson = variables.Get(SpecialVariables.AppliedResources);
            appliedResourcesJson.Should().BeNull();
            log.MessagesVerboseFormatted.Should().Contain(msg => msg.Contains("empty, skipping applied resources"));
        }

        [Test]
        public void WhenReleaseIsPendingUpgrade_RollsBackBeforeUpgrade()
        {
            SetupHelmRollbackMock();

            var deployment = CreateRunningDeployment(CreateVariables());
            var executor = CreateExecutor(deployment);

            executor.RecoverFromPendingRelease(ReleaseName, "pending-upgrade", RevisionNumber);

            commandLineRunner.Received().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("rollback") && i.Arguments.Contains(ReleaseName)));
        }

        [Test]
        public void WhenReleaseIsPendingInstall_UninstallsBeforeUpgrade()
        {
            SetupHelmUninstallMock();

            var deployment = CreateRunningDeployment(CreateVariables());
            var executor = CreateExecutor(deployment);

            executor.RecoverFromPendingRelease(ReleaseName, "pending-install", RevisionNumber);

            commandLineRunner.Received().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("uninstall") && i.Arguments.Contains(ReleaseName)));
        }

        [Test]
        public void WhenReleaseIsDeployed_DoesNotRollbackOrUninstall()
        {
            var deployment = CreateRunningDeployment(CreateVariables());
            var executor = CreateExecutor(deployment);

            executor.RecoverFromPendingRelease(ReleaseName, "deployed", RevisionNumber);

            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("rollback")));
            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("uninstall")));
        }

        [Test]
        public void WhenReleaseIsFailed_DoesNotRollbackOrUninstall()
        {
            var deployment = CreateRunningDeployment(CreateVariables());
            var executor = CreateExecutor(deployment);

            executor.RecoverFromPendingRelease(ReleaseName, "failed", RevisionNumber);

            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("rollback")));
            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("uninstall")));
        }

        [Test]
        public void WhenRollbackFails_LogsWarningAndContinues()
        {
            SetupHelmRollbackMock(exitCode: 1);

            var deployment = CreateRunningDeployment(CreateVariables());
            var executor = CreateExecutor(deployment);

            executor.RecoverFromPendingRelease(ReleaseName, "pending-upgrade", RevisionNumber);

            log.MessagesWarnFormatted.Should().Contain(msg => msg.Contains(ReleaseName) && msg.Contains("pending-upgrade"));
            log.MessagesWarnFormatted.Should().Contain(msg => msg.Contains("non-zero exit code"));
        }

        [Test]
        public void WhenUninstallFails_LogsWarningAndContinues()
        {
            SetupHelmUninstallMock(exitCode: 1);

            var deployment = CreateRunningDeployment(CreateVariables());
            var executor = CreateExecutor(deployment);

            executor.RecoverFromPendingRelease(ReleaseName, "pending-install", RevisionNumber);

            log.MessagesWarnFormatted.Should().Contain(msg => msg.Contains(ReleaseName) && msg.Contains("pending-install"));
            log.MessagesWarnFormatted.Should().Contain(msg => msg.Contains("non-zero exit code"));
        }

        [Test]
        public void WhenReleaseIsPendingUpgrade_ReturnsIncrementedRevision()
        {
            SetupHelmRollbackMock();

            var deployment = CreateRunningDeployment(CreateVariables());
            var executor = CreateExecutor(deployment);

            var result = executor.RecoverFromPendingRelease(ReleaseName, "pending-upgrade", RevisionNumber);

            result.Should().Be(RevisionNumber + 1);
        }

        [Test]
        public void WhenReleaseIsPendingInstall_ReturnsRevisionOne()
        {
            SetupHelmUninstallMock();

            var deployment = CreateRunningDeployment(CreateVariables());
            var executor = CreateExecutor(deployment);

            var result = executor.RecoverFromPendingRelease(ReleaseName, "pending-install", RevisionNumber);

            result.Should().Be(1);
        }

        void SetupChartDirectory()
        {
            Directory.CreateDirectory(ChartDirectory);
            File.WriteAllText(Path.Combine(ChartDirectory, "Chart.yaml"), "apiVersion: v2\nname: test-chart\nversion: 1.0.0");
        }

        void SetupHelmVersionMock()
        {
            //helm 4 removed the --client flag from `helm version`, so the mocked invocation
            //(and its output) reflects a Helm 4 client to prove the executor still works with it.
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("version")))
                             .Returns(info =>
                             {
                                 var invocation = (CommandLineInvocation)info[0];
                                 invocation.AdditionalInvocationOutputSink?.WriteInfo("v4.2.0");
                                 return new CommandResult("helm version", 0);
                             });
        }

        void SetupHelmGetMetadataMock(int revision, string status)
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("get") && i.Arguments.Contains("metadata")))
                             .Returns(info =>
                             {
                                 var invocation = (CommandLineInvocation)info[0];
                                 invocation.AdditionalInvocationOutputSink?.WriteInfo($"{{\"revision\":{revision},\"status\":\"{status}\"}}");
                                 return new CommandResult("helm get metadata", 0);
                             });
        }

        void SetupHelmRollbackMock(int exitCode = 0)
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("rollback")))
                             .Returns(new CommandResult("helm rollback", exitCode));
        }

        void SetupHelmUninstallMock(int exitCode = 0)
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("uninstall")))
                             .Returns(new CommandResult("helm uninstall", exitCode));
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
