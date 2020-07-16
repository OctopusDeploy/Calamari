using System;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class JsonConfigurationVariablesConventionFixture
    {
        void RunTest(
            bool jsonVariablesEnabled,
            bool structuredVariablesEnabled,
            Action<IStructuredConfigVariablesService> assertions
        )
        {
            var variables = new CalamariVariables();
            if (jsonVariablesEnabled)
            {
                variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
            }

            if (structuredVariablesEnabled)
            {
                variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
            }
            
            var service = Substitute.For<IStructuredConfigVariablesService>();
            var convention = new JsonConfigurationVariablesConvention(service);
            var deployment = new RunningDeployment("", variables);
            
            convention.Install(deployment);
            assertions(service);
        }

        [Test]
        public void ShouldCallDoJsonVariableReplacementIfJsonVariableIsSet()
        {
            RunTest(
                jsonVariablesEnabled: true,
                structuredVariablesEnabled: false,
                service => service.Received()
                    .DoJsonVariableReplacement(Arg.Any<RunningDeployment>())
            );
        }
        
        [Test]
        public void ShouldCallDoStructuredVariableReplacementIfStructuredVariableIsSet()
        {
            RunTest(
                jsonVariablesEnabled: false,
                structuredVariablesEnabled: true,
                service => service.Received()
                    .DoStructuredVariableReplacement(Arg.Any<RunningDeployment>())
            );
        }

        [Test]
        public void ShouldNotCallServiceIfNeitherJsonNorStructuredVariablesAreSet()
        {
            RunTest(
                jsonVariablesEnabled: false,
                structuredVariablesEnabled: false,
                service => service.DidNotReceive()
                    .DoStructuredVariableReplacement(Arg.Any<RunningDeployment>())
            );
        }

        [Test]
        public void ShouldPreferJsonOverStructuredVariables()
        {
            RunTest(
                jsonVariablesEnabled: true,
                structuredVariablesEnabled: true,
                service =>
                {
                    service.Received()
                        .DoJsonVariableReplacement(Arg.Any<RunningDeployment>());
                    
                    service.DidNotReceive()
                        .DoStructuredVariableReplacement(Arg.Any<RunningDeployment>());
                });
        }
    }
}
