using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Extensibility.Actions.Sashimi;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Azure.Accounts.Web
{
    class AzureStorageAccountsListAction : AzureActionBase, IAccountDetailsEndpoint
    {
        static readonly BadRequestRegistration OnlyServicePrincipalSupported = new BadRequestRegistration("Account must be an Azure Service Principal Account.");
        static readonly OctopusJsonRegistration<ICollection<AzureStorageAccountResource>> Results = new OctopusJsonRegistration<ICollection<AzureStorageAccountResource>>();

        readonly IOctopusHttpClientFactory httpClientFactory;

        public AzureStorageAccountsListAction(ISystemLog systemLog, IOctopusHttpClientFactory httpClientFactory) : base(systemLog)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public string Method => "GET";
        public string Route => "storageAccounts";
        public string Description => "Lists the storage accounts associated with an Azure account.";

        public async Task<IOctoResponseProvider> Respond(IOctoRequest request, string accountName, AccountDetails accountDetails)
        {
            if (accountDetails.AccountType != AccountTypes.AzureServicePrincipalAccountType)
                return OnlyServicePrincipalSupported.Response();

            var storageAccounts = await GetStorageAccountsAsync(accountName, (AzureServicePrincipalAccountDetails) accountDetails);
            return Results.Response(storageAccounts);
        }

        Task<AzureStorageAccountResource[]> GetStorageAccountsAsync(string accountName, AzureServicePrincipalAccountDetails accountDetails)
        {
            return ThrowIfNotSuccess(async () =>
                {
                    using (var azureClient = accountDetails.CreateStorageManagementClient(httpClientFactory.HttpClientHandler))
                    {
                        return await azureClient.StorageAccounts.ListWithHttpMessagesAsync();
                    }
                }, response => response.Body
                    .Select(service => new AzureStorageAccountResource {Name = service.Name, Location = service.Location}).ToArray(),
                $"Failed to retrieve list of StorageAccounts for '{accountName}' service principal.");
        }
    }
}