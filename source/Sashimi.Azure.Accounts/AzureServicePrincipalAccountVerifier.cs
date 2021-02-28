using System;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Azure.Accounts
{
    class AzureServicePrincipalAccountVerifier : IVerifyAccount
    {
        readonly Lazy<IOctopusHttpClientFactory> httpClientFactoryLazy;

        public AzureServicePrincipalAccountVerifier(Lazy<IOctopusHttpClientFactory> httpClientFactoryLazy)
        {
            this.httpClientFactoryLazy = httpClientFactoryLazy;
        }
        public void Verify(AccountDetails account)
        {
            var typedAccount = (AzureServicePrincipalAccountDetails) account;
            typedAccount.InvalidateTokenCache(httpClientFactoryLazy.Value.HttpClientHandler);

            using var resourcesClient = typedAccount.CreateResourceManagementClient(httpClientFactoryLazy.Value.HttpClientHandler);
            resourcesClient.ResourceGroups.ListWithHttpMessagesAsync().GetAwaiter().GetResult();
        }
    }
}