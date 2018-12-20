#if AZURE
using Calamari.Azure.Integration;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AzureFixtures
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
            var variables = new CalamariVariableDictionary
            {
                {SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, connectionEndpoint}
            };
            var target = new AzureServiceFabricPowerShellContext(variables);
            var actual = target.IsEnabled(syntax);
            actual.Should().Be(expected);

        }
    }
}
#endif