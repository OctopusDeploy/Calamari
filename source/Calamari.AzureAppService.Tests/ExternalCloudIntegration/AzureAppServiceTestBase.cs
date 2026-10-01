using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using NUnit.Framework;
using Octostache;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;

namespace Calamari.AzureAppService.Tests.ExternalCloudIntegration
{
    // Authenticates and builds the ArmClient in a OneTimeSetUp that runs before any derived one, so subclasses
    // can provision or look up resources against it. Carries the ExternalCloudIntegration category for descendants.
    [Category(TestCategory.ExternalCloudIntegration)]
    public abstract class AzureAppServiceTestBase
    {
        protected string ClientId { get; private set; }
        protected string ClientSecret { get; private set; }
        protected string TenantId { get; private set; }
        protected string SubscriptionId { get; private set; }

        protected AzureServicePrincipalAccount ServicePrincipalAccount { get; private set; }
        protected ArmClient ArmClient { get; private set; }
        protected SubscriptionResource SubscriptionResource { get; private set; }

        // Set by the derived class once it has created (dynamic) or looked up (static) its resource group.
        protected ResourceGroupResource ResourceGroupResource { get; set; }

        static readonly CancellationTokenSource CancellationTokenSource = new();
        protected CancellationToken CancellationToken => CancellationTokenSource.Token;

        [OneTimeSetUp]
        public async Task AuthenticateWithAzure()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            ClientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, CancellationToken);
            ClientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, CancellationToken);
            TenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, CancellationToken);
            SubscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, CancellationToken);

            ServicePrincipalAccount = new AzureServicePrincipalAccount(SubscriptionId,
                                                                       ClientId,
                                                                       TenantId,
                                                                       ClientSecret,
                                                                       "AzureGlobalCloud",
                                                                       resourceManagementEndpointBaseUri,
                                                                       activeDirectoryEndpointBaseUri);

            ArmClient = ServicePrincipalAccount.CreateArmClient(retryOptions =>
                                                               {
                                                                   retryOptions.MaxRetries = 5;
                                                                   retryOptions.Mode = RetryMode.Exponential;
                                                                   retryOptions.Delay = TimeSpan.FromSeconds(2);
                                                                   // AzureAppServiceDeployContainerBehaviorFixture.AzureLinuxContainerSlotDeploy occasional timeout at default 100 seconds
                                                                   retryOptions.NetworkTimeout = TimeSpan.FromSeconds(200);
                                                               });

            SubscriptionResource = ArmClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));
        }

        protected void AddAzureVariables(CommandTestBuilderContext context) => AddAzureVariables(context.Variables);

        protected virtual void AddAzureVariables(VariableDictionary variables)
        {
            variables.Add(AccountVariables.ClientId, ClientId);
            variables.Add(AccountVariables.Password, ClientSecret);
            variables.Add(AccountVariables.TenantId, TenantId);
            variables.Add(AccountVariables.SubscriptionId, SubscriptionId);
        }
    }
}