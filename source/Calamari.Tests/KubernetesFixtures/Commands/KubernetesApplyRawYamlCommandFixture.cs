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
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;
using NSubstitute;

namespace Calamari.Tests.KubernetesFixtures.Commands
{
    [TestFixture]
    public class KubernetesApplyRawYamlCommandFixture
    {
        [Test]
        public void WhenResourceStatusIsDisabled_ShouldNotRunStatusChecks()
        {
            var variables = new CalamariVariables()
            {
                [KnownVariables.EnabledFeatureToggles] = "MultiGlobPathsForRawYamlFeatureToggle",
                [SpecialVariables.ResourceStatusCheck] = "False"
            };
            var resourceStatusCheck = Substitute.For<IResourceStatusReportExecutor>();
            var command = CreateCommand(variables, resourceStatusCheck);

            command.Execute(new string[]{ });

            resourceStatusCheck.ReceivedCalls().Should().BeEmpty();
        }

        [Test]
        public void WhenResourceStatusIsEnabled_ShouldRunStatusChecks()
        {
            var variables = new CalamariVariables()
            {
                [KnownVariables.EnabledFeatureToggles] = "MultiGlobPathsForRawYamlFeatureToggle",
                [SpecialVariables.ResourceStatusCheck] = "True"
            };
            var resourceStatusCheck = Substitute.For<IResourceStatusReportExecutor>();
            var command = CreateCommand(variables, resourceStatusCheck);

            command.Execute(new string[]{ });

            resourceStatusCheck.ReceivedCalls().Should().HaveCount(1);
        }

        private KubernetesApplyRawYamlCommand CreateCommand(IVariables variables, IResourceStatusReportExecutor resourceStatusCheck)
        {
            var log = new InMemoryLog();
            var fs = new TestCalamariPhysicalFileSystem();
            var kubectl = new Kubectl(variables, log, Substitute.For<ICommandLineRunner>());

            return new KubernetesApplyRawYamlCommand(
                log,
                Substitute.For<IDeploymentJournalWriter>(),
                variables,
                fs,
                Substitute.For<IExtractPackage>(),
                Substitute.For<ISubstituteInFiles>(),
                Substitute.For<IStructuredConfigVariablesService>(),
                Substitute.For<IRawYamlKubernetesApplyExecutor>(),
                resourceStatusCheck,
                kubectl);
        }
    }
}