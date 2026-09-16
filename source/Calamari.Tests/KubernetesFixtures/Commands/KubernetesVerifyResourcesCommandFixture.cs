using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Commands
{
    [TestFixture]
    public class KubernetesVerifyResourcesCommandFixture
    {
        [Test]
        public void WhenNoAppliedResourcesVariableIsSet_ShouldThrowCommandException()
        {
            var variables = new CalamariVariables();
            var statusReporter = Substitute.For<IResourceStatusReportExecutor>();
            var command = CreateCommand(variables, statusReporter, new InMemoryLog());

            Action execute = () => command.Execute(new string[] { });

            execute.Should()
                   .Throw<CommandException>()
                   .WithMessage("The applied resources variable was not found. This variable is required to verify the deployed resources.");
            statusReporter.ReceivedCalls().Should().BeEmpty();
        }

        [Test]
        public void WhenAppliedResourcesIsAnEmptyList_ShouldDoNothingAndSucceed()
        {
            var variables = new CalamariVariables
            {
                [SpecialVariables.AppliedResources] = "[]"
            };
            var statusReporter = Substitute.For<IResourceStatusReportExecutor>();
            var log = new InMemoryLog();
            var command = CreateCommand(variables, statusReporter, log);

            var result = command.Execute(new string[] { });

            result.Should().Be(0);
            statusReporter.ReceivedCalls().Should().BeEmpty();
            log.MessagesInfoFormatted.Should().Contain("Applied resources list is empty; nothing to verify.");
        }

        [Test]
        public void WhenAppliedResourcesIsNotValidJson_ShouldThrowCommandException()
        {
            var variables = new CalamariVariables
            {
                [SpecialVariables.AppliedResources] = "this is not json"
            };
            var statusReporter = Substitute.For<IResourceStatusReportExecutor>();
            var command = CreateCommand(variables, statusReporter, new InMemoryLog());

            Action execute = () => command.Execute(new string[] { });

            execute.Should()
                   .Throw<CommandException>()
                   .WithMessage("Could not parse applied resources output variable:*");
            statusReporter.ReceivedCalls().Should().BeEmpty();
        }

        static KubernetesVerifyResourcesCommand CreateCommand(
            IVariables variables,
            IResourceStatusReportExecutor statusReporter,
            InMemoryLog log)
        {
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var kubectl = new Kubectl(variables, log, commandLineRunner);

            return new KubernetesVerifyResourcesCommand(
                log,
                variables,
                fileSystem,
                commandLineRunner,
                kubectl,
                statusReporter);
        }
    }
}
