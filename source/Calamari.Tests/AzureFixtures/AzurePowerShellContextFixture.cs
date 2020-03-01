#if AZURE_CORE
using Calamari.Azure.Integration;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AzureFixtures
{
    [TestFixture]
    public class AzurePowerShellContextFixture
    {
        [Test]
        [TestCase("Azure", "", ScriptSyntax.PowerShell, true)]
        [TestCase("Nope", "", ScriptSyntax.PowerShell, false)]
        [TestCase("Azure", "Nope", ScriptSyntax.PowerShell, false)]
        [TestCase("Azure", "", ScriptSyntax.FSharp, false)]
        public void ShouldBeEnabled(string accountType, string connectionEndpoint, ScriptSyntax syntax, bool expected)
        {
            var variables = new CalamariVariableDictionary
            {
                {SpecialVariables.Account.AccountType, accountType},
                {SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, connectionEndpoint}
            };
            var target = new AzurePowerShellContext(variables);
            var actual = target.IsEnabled(syntax);
            actual.Should().Be(expected);
        }
    }
}
#endif