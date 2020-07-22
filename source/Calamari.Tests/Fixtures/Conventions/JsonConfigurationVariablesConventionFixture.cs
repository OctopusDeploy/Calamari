using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class JsonConfigurationVariablesConventionFixture
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
            var convention = new JsonConfigurationVariablesConvention(service);
            convention.Install(deployment);
            service.DidNotReceiveWithAnyArgs().ReplaceVariables(deployment);
        }

        [Test]
        public void ShouldNotRunIfVariableIsFalse()
        {
            var convention = new JsonConfigurationVariablesConvention(service);
            deployment.Variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, false);
            convention.Install(deployment);
            service.DidNotReceiveWithAnyArgs().ReplaceVariables(deployment);
        }

        [Test]
        public void ShouldRunIfVariableIsTrue()
        {
            var convention = new JsonConfigurationVariablesConvention(service);
            deployment.Variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
            convention.Install(deployment);
            service.Received().ReplaceVariables(deployment);
        }
    }
}
