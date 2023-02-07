using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.AzureServiceFabric.Integration;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AzureServiceFabric.Tests
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
                { SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, connectionEndpoint }
            };
            var target = new AzureServiceFabricPowerShellContext(variables, ConsoleLog.Instance);
            var actual = target.IsEnabled(syntax);
            actual.Should().Be(expected);
        }
    }
}