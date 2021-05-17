using System;
using System.Threading;
using System.Threading.Tasks;
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
        public async Task Verify(AccountDetails account, CancellationToken cancellationToken)
        {
            var typedAccount = (AzureServicePrincipalAccountDetails) account;
            typedAccount.InvalidateTokenCache(httpClientFactoryLazy.Value.HttpClientHandler);

            using var resourcesClient = typedAccount.CreateResourceManagementClient(httpClientFactoryLazy.Value.HttpClientHandler);
            await resourcesClient.ResourceGroups.ListWithHttpMessagesAsync(cancellationToken: cancellationToken);
        }
    }
}