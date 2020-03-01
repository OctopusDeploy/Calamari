using Calamari.Azure.ServiceFabric.Integration;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Variables;
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
            var variables = new CalamariVariables
            {
                {SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, connectionEndpoint}
            };
            var target = new AzureServiceFabricPowerShellContext(variables);
            var actual = target.IsEnabled(syntax);
            actual.Should().Be(expected);

        }
    }
}