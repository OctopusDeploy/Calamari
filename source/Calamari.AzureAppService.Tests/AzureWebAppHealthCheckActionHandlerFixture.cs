#nullable disable
using System;
using System.Net;
using System.Threading.Tasks;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    [NonParallelizable]
    class AzureWebAppHealthCheckActionHandlerFixture : AppServiceIntegrationTest
    {
        const string NonExistentProxyHostname = "non-existent-proxy.local";
        const int NonExistentProxyPort = 3128;
        readonly IWebProxy originalProxy = WebRequest.DefaultWebProxy;
        readonly string originalProxyHost = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost);
        readonly string originalProxyPort = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort);

        protected override async Task ConfigureTestResources(ResourceGroupResource resourceGroup)
        {
            var (_, webSiteResource) = await CreateAppServicePlanAndWebApp(resourceGroup,
                                                                           new AppServicePlanData(resourceGroup.Data.Location)
                                                                           {
                                                                               Sku = new AppServiceSkuDescription
                                                                               {
                                                                                   Name = "B1",
                                                                                   Tier = "Basic"
                                                                               }
                                                                           },
                                                                           new WebSiteData(resourceGroup.Data.Location)
                                                                           {
                                                                               SiteConfig = new SiteConfigProperties
                                                                               {
                                                                                   NetFrameworkVersion = "v6.0"
                                                                               }
                                                                           });
            WebSiteResource = webSiteResource;
        }

        public override async Task Cleanup()
        {
            RestoreLocalEnvironmentProxySettings();
            RestoreCiEnvironmentProxySettings();

            await base.Cleanup();
        }

        [Test]
        [NonParallelizable]
        public async Task WebAppIsFound_WithAndWithoutProxy()
        {
            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(SetUpVariables)
                                    .WithAssert(result => result.WasSuccessful.Should().BeTrue())
                                    .Execute();

            // Here we verify whether the proxy is correctly picked up
            // Since the proxy we use here is non-existent, health check to the same Web App should fail due this this proxy setting
            SetLocalEnvironmentProxySettings(NonExistentProxyHostname, NonExistentProxyPort);
            SetCiEnvironmentProxySettings(NonExistentProxyHostname, NonExistentProxyPort);
            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(SetUpVariables)
                                    .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                                    .Execute(false);
        }

        [Test]
        [NonParallelizable]
        public async Task WebAppIsNotFound()
        {
            var randomName = Randomizer.CreateRandomizer().GetString(34, "abcdefghijklmnopqrstuvwxyz1234567890");
            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(SetUpVariables)
                                    .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                                    .Execute(false);
        }

        static void SetLocalEnvironmentProxySettings(string hostname, int port)
        {
            var proxySettings = new UseCustomProxySettings(hostname, port, null!, null!).CreateProxy().Value;
            WebRequest.DefaultWebProxy = proxySettings;
        }

        static void SetCiEnvironmentProxySettings(string hostname, int port)
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, hostname);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, $"{port}");
        }

        void RestoreLocalEnvironmentProxySettings()
        {
            WebRequest.DefaultWebProxy = originalProxy;
        }

        void RestoreCiEnvironmentProxySettings()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, originalProxyHost);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, originalProxyPort);
        }

        void SetUpVariables(CommandTestBuilderContext context)
        {
            AddAzureVariables(context.Variables);
            context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
        }
    }
}