#nullable disable
using System;
using System.Net;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Calamari.AzureAppService.Tests.ExternalCloudIntegration
{
    [TestFixture]
    class AzureAppHealthCheckActionHandlerFixture : AzureAppServiceWithStaticResourcesTestBase
    {
        // https://portal.azure.com/#@octopusdeploy.onmicrosoft.com/resource/subscriptions/cf21dc34-73dc-4d7d-bd86-041884e0bc75/resourcegroups/calamari-testing-static-rg/providers/Microsoft.Web/sites/calamari-testing-static-health-check/appServices
        const string WebAppName = "calamari-testing-static-health-check";
        const string ExistingSlotName = "stage";
        const string NonExistentSlotName = "not-a-slot";
        
        const string NonExistentProxyHostname = "non-existent-proxy.local";
        const int NonExistentProxyPort = 3128;

        [Test]
        public async Task WebApp_Exists_ReturnsSuccessful()
        {
            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(SetUpVariables)
                                    .WithAssert(result => result.WasSuccessful.Should().BeTrue())
                                    .Execute();
        }
        
        [Test]
        public async Task WebApp_DoesNotExist_ReturnsUnsuccessful()
        {
            var randomName = Randomizer.CreateRandomizer().GetString(34, "abcdefghijklmnopqrstuvwxyz1234567890");
            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     SetUpVariables(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, randomName);
                                                 })
                                    .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                                    .Execute(false);
        }
        
        [Test]
        public async Task WebAppSlot_Exists_ReturnsSuccessful()
        {
            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     SetUpVariables(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.WebAppSlot, ExistingSlotName);
                                                 })
                                    .WithAssert(result => result.WasSuccessful.Should().BeTrue())
                                    .Execute();
        }
        
        [Test]
        public async Task WebAppSlot_DoesNotExist_ReturnsUnsuccessful()
        {
            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     SetUpVariables(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.WebAppSlot, NonExistentSlotName);
                                                 })
                                    .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                                    .Execute(false);
        }

        [Test]
        [NonParallelizable]
        public async Task WebApp_ExistsButWithInvalidProxy_ReturnsUnsuccessful()
        {
            var originalProxy = WebRequest.DefaultWebProxy;
            var originalProxyHost = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost);
            var originalProxyPort = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort);
            
            // Here we verify whether the proxy is correctly picked up
            // Since the proxy we use here is non-existent, health check to the same Web App should fail due to this proxy setting
            SetLocalEnvironmentProxySettings(NonExistentProxyHostname, NonExistentProxyPort);
            SetCiEnvironmentProxySettings(NonExistentProxyHostname, NonExistentProxyPort);
            
            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(SetUpVariables)
                                    .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                                    .Execute(false);
            
            RestoreCiEnvironmentProxySettings(originalProxyHost,originalProxyPort);
            RestoreLocalEnvironmentProxySettings(originalProxy);
        }
        
        static void SetLocalEnvironmentProxySettings(string hostname, int port)
        {
            var proxySettings = new UseCustomProxySettings(hostname, port, null!, null!).CreateProxy().Value;
            WebRequest.DefaultWebProxy = proxySettings;
        }

        static void SetCiEnvironmentProxySettings(string hostname, int port)
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, hostname);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, port.ToString());
        }

        void RestoreLocalEnvironmentProxySettings(IWebProxy originalProxy)
        {
            WebRequest.DefaultWebProxy = originalProxy;
        }

        void RestoreCiEnvironmentProxySettings(string originalHost, string originalPort)
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, originalHost);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, originalPort);
        }

        void SetUpVariables(CommandTestBuilderContext context)
        {
            AddAzureVariables(context.Variables);
            
            context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
            context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, WebAppName);
        }
    }
}