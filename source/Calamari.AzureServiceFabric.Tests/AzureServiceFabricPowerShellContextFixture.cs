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
        
        // TODO: This is temporary and should not be checked in
        [Test]
        public async Task TokenScratch()
        {
            
            var options = new Microsoft.Identity.Client.ConfidentialClientApplicationOptions()
            {
                ClientId = "clientId",
                ClientSecret = "ClientSecret"
            };
            var clientApplicationContext = Microsoft.Identity.Client.ConfidentialClientApplicationBuilder
                                                    .CreateWithApplicationOptions(options)
                                                    .Build();
            // Note: Scopes, need to check this, resourceUriId/.default should default ot the resource scopes.
            var scopes = new List<string>() { "resourceUriId/.default" };
            var authContext = await clientApplicationContext.AcquireTokenForClient(scopes).ExecuteAsync();
            var token = authContext.AccessToken;

        }
        
        // TODO: This is temporary and should not be checked in
        [Test]
        public async Task TokenScratchUserAndPassword()
        {
            var scopes = new List<string>() { "resourceUriId/.default" };
            
            var clientApplicationContext = Microsoft.Identity.Client.PublicClientApplicationBuilder
                                                    .Create("clientId")
                                                    .Build();
            // Note: Scopes, need to check this, resourceUriId/.default should default ot the resource scopes.
            var authContext = await clientApplicationContext.AcquireTokenByUsernamePassword(scopes, "username", "password")
                                                            .ExecuteAsync();
            var token = authContext.AccessToken;
            
        }
        
        
        
    }
}