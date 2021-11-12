using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Conventions
{
    [TestFixture]
    public class StructuredConfigurationVariablesConventionFixture
    {
        RunningDeployment deployment;
        IStructuredConfigVariablesService service;

        [SetUp]
        public void SetUp()
        {
            var variables = new CalamariVariables();
            service = Substitute.For<IStructuredConfigVariablesService>();
            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("Packages"), variables);
        }

        [Test]
        public void ShouldNotRunIfVariableNotSet()
        {
            var convention = new StructuredConfigurationVariablesConvention(new StructuredConfigurationVariablesBehaviour(service));
            convention.Install(deployment);
            service.DidNotReceiveWithAnyArgs().ReplaceVariables(deployment.CurrentDirectory);
        }

        [Test]
        public void ShouldNotRunIfVariableIsFalse()
        {
            var convention = new StructuredConfigurationVariablesConvention(new StructuredConfigurationVariablesBehaviour(service));
            convention.Install(deployment);
            service.DidNotReceiveWithAnyArgs().ReplaceVariables(deployment.CurrentDirectory);
        }

        [Test]
        public void ShouldRunIfVariableIsTrue()
        {
            var convention = new StructuredConfigurationVariablesConvention(new StructuredConfigurationVariablesBehaviour(service));
            deployment.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
            convention.Install(deployment);
            service.Received().ReplaceVariables(deployment.CurrentDirectory);
        }
    }
}
