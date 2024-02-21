using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands;
using Calamari.Kubernetes.Commands.Executors;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;
using NSubstitute;

namespace Calamari.Tests.KubernetesFixtures.Commands
{
    [TestFixture]
    public class KubernetesKustomizeCommandFixture
    {
        [Test]
        public void WhenResourceStatusIsDisabled_ShouldNotRunStatusChecks()
        {
            // Arrange
            var variables = new CalamariVariables()
            {
                [SpecialVariables.ResourceStatusCheck] = "False"
            };
            var resourceStatusCheck = Substitute.For<IResourceStatusReportExecutor>();
            var command = CreateCommand(variables, resourceStatusCheck);

            // Act
            var result = command.Execute(new string[]{ });
            
            // Assert
            resourceStatusCheck.ReceivedCalls().Should().BeEmpty();
            result.Should().Be(0);
        }

        [Test]
        [TestCase(5, true)]
        [TestCase(10, false)]
        public void WhenResourceStatusIsEnabled_ShouldRunStatusChecks(int timeout, bool waitForJobs)
        {
            // Arrange
            var variables = new CalamariVariables()
            {
                [SpecialVariables.ResourceStatusCheck] = "True",
                [SpecialVariables.Timeout] = timeout.ToString(),
                [SpecialVariables.WaitForJobs] = waitForJobs.ToString()
            };
            var runningCheck = Substitute.For<IRunningResourceStatusCheck>();
            runningCheck.WaitForCompletionOrTimeout().Returns(true);
            var resourceStatusCheck = Substitute.For<IResourceStatusReportExecutor>();
            resourceStatusCheck.Start(Arg.Any<int>(), Arg.Any<bool>()).Returns(runningCheck);
            var command = CreateCommand(variables, resourceStatusCheck);

            // Act
            var result = command.Execute(new string[]{ });

            // Assert
            resourceStatusCheck.ReceivedCalls().Should().HaveCount(1);
            resourceStatusCheck.Received().Start(timeout, waitForJobs);
            runningCheck.Received().WaitForCompletionOrTimeout();
            result.Should().Be(0);
        }

        KubernetesKustomizeCommand CreateCommand(IVariables variables, IResourceStatusReportExecutor resourceStatusCheck)
        {
            var log = new InMemoryLog();
            var fs = new TestCalamariPhysicalFileSystem();
            var kubectl = new Kubectl(variables, log, Substitute.For<ICommandLineRunner>());
            var kubernetesApplyExecutor = Substitute.For<IKustomizeKubernetesApplyExecutor>();
            kubernetesApplyExecutor.Execute(Arg.Any<RunningDeployment>(), Arg.Any<Func<ResourceIdentifier[], Task>>()).Returns(true);

            return new KubernetesKustomizeCommand(
                log,
                Substitute.For<IDeploymentJournalWriter>(),
                variables,
                fs,
                Substitute.For<IExtractPackage>(),
                Substitute.For<ISubstituteInFiles>(),
                Substitute.For<IStructuredConfigVariablesService>(),
                kubernetesApplyExecutor,
                resourceStatusCheck,
                kubectl);
        }
    }
}