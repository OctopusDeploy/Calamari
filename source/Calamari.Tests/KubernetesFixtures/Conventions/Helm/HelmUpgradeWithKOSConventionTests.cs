using System.Collections.Generic;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Conventions;
using Calamari.Kubernetes.Helm;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Conventions.Helm
{
    [TestFixture]
    public class HelmUpgradeWithKOSConventionTests
    {
        const string ReleaseName = "my-release";

        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        ICommandLineRunner commandLineRunner;
        InMemoryLog log;
        string tempDirectory;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            commandLineRunner = Substitute.For<ICommandLineRunner>();
            tempDirectory = fileSystem.CreateTemporaryDirectory();

            SetupKubectlMocks();
            SetupHelmVersionMock();
            SetupHelmUpgradeMock();
        }

        [TearDown]
        public void TearDown()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void WhenReleaseIsPendingUpgrade_RollsBackBeforeUpgrade()
        {
            SetupHelmGetMetadataMock(revision: 2);
            SetupHelmStatusMock("pending-upgrade");

            RunInstall();

            commandLineRunner.Received().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("rollback") && i.Arguments.Contains(ReleaseName)));
        }

        [Test]
        public void WhenReleaseIsPendingInstall_UninstallsBeforeUpgrade()
        {
            SetupHelmGetMetadataMock(revision: 1);
            SetupHelmStatusMock("pending-install");

            RunInstall();

            commandLineRunner.Received().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("uninstall") && i.Arguments.Contains(ReleaseName)));
        }

        [Test]
        public void WhenReleaseIsDeployed_DoesNotRollbackOrUninstall()
        {
            SetupHelmGetMetadataMock(revision: 3);
            SetupHelmStatusMock("deployed");

            RunInstall();

            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("rollback")));
            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("uninstall")));
        }

        [Test]
        public void WhenReleaseIsFailed_DoesNotRollbackOrUninstall()
        {
            SetupHelmGetMetadataMock(revision: 2);
            SetupHelmStatusMock("failed");

            RunInstall();

            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("rollback")));
            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("uninstall")));
        }

        [Test]
        public void WhenReleaseDoesNotExist_DoesNotCheckStatusOrRollbackOrUninstall()
        {
            // GetCurrentRevision returns null — release has never been installed
            SetupHelmGetMetadataToReturnNotFound();

            RunInstall();

            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("status") && i.Arguments.Contains(ReleaseName)));
            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("rollback")));
            commandLineRunner.DidNotReceive().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("uninstall")));
        }

        [Test]
        public void WhenRollbackFails_LogsWarningAndContinuesToUpgrade()
        {
            SetupHelmGetMetadataMock(revision: 2);
            SetupHelmStatusMock("pending-upgrade");
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("rollback")))
                             .Returns(new CommandResult("helm rollback", 1));

            RunInstall();

            log.MessagesWarnFormatted.Should().Contain(msg => msg.Contains(ReleaseName) && msg.Contains("non-zero exit code"));
            commandLineRunner.Received().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("upgrade")));
        }

        [Test]
        public void WhenUninstallFails_LogsWarningAndContinuesToUpgrade()
        {
            SetupHelmGetMetadataMock(revision: 1);
            SetupHelmStatusMock("pending-install");
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("uninstall")))
                             .Returns(new CommandResult("helm uninstall", 1));

            RunInstall();

            log.MessagesWarnFormatted.Should().Contain(msg => msg.Contains(ReleaseName) && msg.Contains("non-zero exit code"));
            commandLineRunner.Received().Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("upgrade")));
        }

        [Test]
        public void WhenReleaseIsPendingUpgrade_ReReadsRevisionAfterRollback()
        {
            // Rollback creates a new revision, so revision number must be re-read after recovery.
            // We verify this by checking that helm get metadata is called twice.
            SetupHelmGetMetadataMock(revision: 2);
            SetupHelmStatusMock("pending-upgrade");

            RunInstall();

            commandLineRunner.Received(2).Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("get") && i.Arguments.Contains("metadata")));
        }

        void RunInstall()
        {
            var variables = CreateVariables();
            var deployment = CreateRunningDeployment(variables);
            var convention = CreateConvention(deployment);
            convention.Install(deployment);
        }

        void SetupKubectlMocks()
        {
            // Return kubectl path from where/which
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("kubectl")))
                             .Returns(info =>
                             {
                                 var invocation = (CommandLineInvocation)info[0];
                                 invocation.AdditionalInvocationOutputSink?.WriteInfo("kubectl");
                                 return new CommandResult("where kubectl", 0);
                             });

            // Return success for kubectl version --client
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Executable == "kubectl"))
                             .Returns(new CommandResult("kubectl version", 0));
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

        void SetupHelmGetMetadataMock(int revision)
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("get") && i.Arguments.Contains("metadata")))
                             .Returns(info =>
                             {
                                 var invocation = (CommandLineInvocation)info[0];
                                 invocation.AdditionalInvocationOutputSink?.WriteInfo($"{{\"revision\":{revision}}}");
                                 return new CommandResult("helm get metadata", 0);
                             });
        }

        void SetupHelmGetMetadataToReturnNotFound()
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("get") && i.Arguments.Contains("metadata")))
                             .Returns(new CommandResult("helm get metadata", 1));
        }

        void SetupHelmStatusMock(string status)
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("status") && i.Arguments.Contains(ReleaseName)))
                             .Returns(info =>
                             {
                                 var invocation = (CommandLineInvocation)info[0];
                                 invocation.AdditionalInvocationOutputSink?.WriteInfo($"{{\"info\":{{\"status\":\"{status}\"}}}}");
                                 return new CommandResult("helm status", 0);
                             });
        }

        void SetupHelmUpgradeMock()
        {
            commandLineRunner.Execute(Arg.Is<CommandLineInvocation>(i => i.Arguments.Contains("upgrade")))
                             .Returns(new CommandResult("helm upgrade", 0));
        }

        CalamariVariables CreateVariables()
        {
            var variables = new CalamariVariables
            {
                [SpecialVariables.Helm.ReleaseName] = ReleaseName,
                // --dry-run bypasses the manifest reporting path in HelmManifestAndStatusReporter
                [SpecialVariables.Helm.AdditionalArguments] = "--dry-run"
            };
            return variables;
        }

        RunningDeployment CreateRunningDeployment(CalamariVariables variables)
        {
            return new RunningDeployment(variables, new Dictionary<string, string>())
            {
                CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory,
                StagingDirectory = tempDirectory
            };
        }

        HelmUpgradeWithKOSConvention CreateConvention(RunningDeployment deployment)
        {
            var kubectl = new Kubectl(deployment.Variables, log, commandLineRunner, tempDirectory, deployment.EnvironmentVariables);
            var templateValueSourcesParser = new HelmTemplateValueSourcesParser(fileSystem, log);
            var namespaceResolver = Substitute.For<IKubernetesManifestNamespaceResolver>();
            var statusReporter = Substitute.For<IResourceStatusReportExecutor>();
            var manifestReporter = Substitute.For<IManifestReporter>();

            return new HelmUpgradeWithKOSConvention(log,
                                                    commandLineRunner,
                                                    fileSystem,
                                                    templateValueSourcesParser,
                                                    statusReporter,
                                                    manifestReporter,
                                                    namespaceResolver,
                                                    kubectl);
        }
    }
}
