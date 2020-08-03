using Calamari.Azure.ServiceFabric.Integration;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Azure.ServiceFabric.Tests.Fixtures
{
    [TestFixture]
    public class AzureServiceFabricPowerShellContextFixture
    {
        [Test]
        [TestCase("Endpoint", ScriptSyntax.PowerShell, true)]
        [TestCase("", ScriptSyntax.PowerShell, false)]
        [TestCase("Endpoint", ScriptSyntax.FSharp, false)]
        public void ShouldBeEnabled(string connectionEndpoint, ScriptSyntax syntax, bool expected)
        {
            var variables = new CalamariVariables();
            variables.Add(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, connectionEndpoint);
            var target = new AzureServiceFabricPowerShellContext(variables);
            var actual = target.IsEnabled(syntax);
            actual.Should().Be(expected);

        }
    }
}