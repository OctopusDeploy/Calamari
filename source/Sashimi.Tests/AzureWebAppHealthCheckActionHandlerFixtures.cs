#nullable disable
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using NUnit.Framework;
using Sashimi.Azure.Accounts;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.AzureAppService.Tests
{
    [TestFixture]
    class AzureWebAppHealthCheckActionHandlerFixtures
    {
        [Test]
        public async Task WebApp_Is_Found()
        {
            var resourceGroupName = SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60);
            var clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            var clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            var tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId, clientSecret, tenantId,
                AzureEnvironment.AzureGlobalCloud);

            var azure = Microsoft.Azure.Management.Fluent.Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials)
                .WithSubscription(subscriptionId);

            IResourceGroup resourceGroup = null;
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
                await azure.WebApps
                    .Define(webAppName)
                    .WithExistingWindowsPlan(appServicePlan)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithRuntimeStack(WebAppRuntimeStack.NETCore)
                    .CreateAsync();

                ActionHandlerTestBuilder.CreateAsync<AzureWebAppHealthCheckActionHandler, Program>()
                    .WithArrange(context =>
                    {
                        context.Variables.Add(AccountVariables.SubscriptionId,
                            subscriptionId);
                        context.Variables.Add(AccountVariables.TenantId,
                            tenantId);
                        context.Variables.Add(AccountVariables.ClientId,
                            clientId);
                        context.Variables.Add(AccountVariables.Password,
                            clientSecret);
                        context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroupName);
                        context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, webAppName);
                        context.Variables.Add(SpecialVariables.AccountType, AccountTypes.AzureServicePrincipalAccountType.ToString());
                    })
                    .WithAssert(result => result.WasSuccessful.Should().BeTrue())
                    .Execute();
            }
            finally
            {
                if (resourceGroup != null)
                {
                    azure.ResourceGroups.DeleteByNameAsync(resourceGroupName).Ignore();
                }
            }
        }

        [Test]
        public void WebApp_Is_Not_Found()
        {
            var randomName = SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60);
            var clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            var clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            var tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            ActionHandlerTestBuilder.CreateAsync<AzureWebAppHealthCheckActionHandler, Program>()
                .WithArrange(context =>
                {
                    context.Variables.Add(AccountVariables.SubscriptionId,
                        subscriptionId);
                    context.Variables.Add(AccountVariables.TenantId,
                        tenantId);
                    context.Variables.Add(AccountVariables.ClientId,
                        clientId);
                    context.Variables.Add(AccountVariables.Password,
                        clientSecret);
                    context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, randomName);
                    context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, randomName);
                    context.Variables.Add(SpecialVariables.AccountType, AccountTypes.AzureServicePrincipalAccountType.ToString());
                })
                .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                .Execute(false);
        }
    }
}