using System.Threading.Tasks;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using NUnit.Framework;

namespace Calamari.AzureWebApp.Tests
{
    [TestFixture]
    public class DeployAzureWebCommandFixture
    {
        [Test]
        public async Task Deploy_WebApp_Empty()
        {
            var clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            var clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            var tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            var webAppName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);
            var resourceGroupName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);

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

                await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                    .WithArrange(context =>
                    {
                        context.Variables.Add(AzureAccountVariables.SubscriptionId,
                            subscriptionId);
                        context.Variables.Add(AzureAccountVariables.TenantId,
                            tenantId);
                        context.Variables.Add(AzureAccountVariables.ClientId,
                            clientId);
                        context.Variables.Add(AzureAccountVariables.Password,
                            clientSecret);
                        context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, webAppName);
                        context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroupName);
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
    }
}