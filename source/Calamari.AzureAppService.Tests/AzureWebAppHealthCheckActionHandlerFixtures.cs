#nullable disable
using System;
using System.Net;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using NUnit.Framework;
using OperatingSystem = Microsoft.Azure.Management.AppService.Fluent.OperatingSystem;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    [NonParallelizable]
    class AzureWebAppHealthCheckActionHandlerFixtures
    {
        const string NonExistentProxyHostname = "non-existent-proxy.local";
        const int NonExistentProxyPort = 3128;
        readonly IWebProxy originalProxy = WebRequest.DefaultWebProxy;
        readonly string originalProxyHost = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost);
        readonly string originalProxyPort = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort);
        
        [Test]
        [NonParallelizable]
        public async Task WebAppIsFound_WithAndWithoutProxy()
        {
            IAzure azure = null;
            IResourceGroup resourceGroup = null;
            IWebApp webApp;
            try
            {
                (azure, resourceGroup, webApp) = await SetUpAzureWebApp();
                azure.Should().NotBeNull();
                resourceGroup.Should().NotBeNull();
                webApp.Should().NotBeNull();
                
                await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                        .WithArrange(context => SetUpVariables(context, resourceGroup.Name, webApp.Name))
                                        .WithAssert(result => result.WasSuccessful.Should().BeTrue())
                                        .Execute();
                
                // Here we verify whether the proxy is correctly picked up
                // Since the proxy we use here is non-existent, health check to the same Web App should fail due this this proxy setting
                SetLocalEnvironmentProxySettings(NonExistentProxyHostname, NonExistentProxyPort);
                SetCiEnvironmentProxySettings(NonExistentProxyHostname, NonExistentProxyPort);
                await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                        .WithArrange(context => SetUpVariables(context, resourceGroup.Name, webApp.Name))
                                        .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                                        .Execute(false);
            }
            finally
            {
                RestoreLocalEnvironmentProxySettings();
                RestoreCiEnvironmentProxySettings();
                await CleanResources(azure, resourceGroup);
            }
        }

        [Test]
        [NonParallelizable]
        public async Task WebAppIsNotFound()
        {
            var randomName = SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60);
            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(context => SetUpVariables(context, randomName, randomName))
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
        
        static async Task<(IAzure azure, IResourceGroup resourceGroup, IWebApp webApp)> SetUpAzureWebApp()
        {
            var resourceGroupName = SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60);
            var clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            var clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            var tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId,
                                                                                      clientSecret,
                                                                                      tenantId,
                                                                                      AzureEnvironment.AzureGlobalCloud);

            var azure = Microsoft.Azure.Management.Fluent.Azure
                                 .Configure()
                                 .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                                 .Authenticate(credentials)
                                 .WithSubscription(subscriptionId);

            IResourceGroup resourceGroup = null;
            IWebApp webApp = null;
            try
            {
                resourceGroup = await azure.ResourceGroups
                                           .Define(resourceGroupName)
                                           .WithRegion(Region.USWest)
                                           .CreateAsync();

                var appServicePlan = await azure.AppServices.AppServicePlans
                                                .Define(SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60))
                                                .WithRegion(resourceGroup.Region)
                                                .WithExistingResourceGroup(resourceGroup)
                                                .WithPricingTier(PricingTier.BasicB1)
                                                .WithOperatingSystem(OperatingSystem.Windows)
                                                .CreateAsync();

                var webAppName = SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60);
                webApp = await azure.WebApps
                           .Define(webAppName)
                           .WithExistingWindowsPlan(appServicePlan)
                           .WithExistingResourceGroup(resourceGroup)
                           .WithRuntimeStack(WebAppRuntimeStack.NETCore)
                           .CreateAsync();

                return (azure, resourceGroup, webApp);
            }
            catch
            {
                // When errors encountered in set up, we return the resources that have been set up so far anyway
                // We will clean the resources from the caller
                return (azure, resourceGroup, webApp);
            }
        }

        static void SetUpVariables(CommandTestBuilderContext context, string resourceGroupName, string webAppName)
        {
            var clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            var clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            var tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            
            context.Variables.Add(AccountVariables.SubscriptionId, subscriptionId);
            context.Variables.Add(AccountVariables.TenantId, tenantId);
            context.Variables.Add(AccountVariables.ClientId, clientId);
            context.Variables.Add(AccountVariables.Password, clientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroupName);
            context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, webAppName);
            context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
        }

        static async Task CleanResources(IAzure azure, IResourceGroup resourceGroup)
        {
            if (azure != null && resourceGroup != null)
            {
                await azure.ResourceGroups.DeleteByNameAsync(resourceGroup.Name);
            }
        }
    }
}