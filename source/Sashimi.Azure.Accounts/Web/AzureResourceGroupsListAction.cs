using System;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Extensibility.Actions.Sashimi;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Azure.Accounts.Web
{
    class AzureResourceGroupsListAction : AzureActionBase, IAccountDetailsEndpoint
    {
        static readonly BadRequestRegistration OnlyServicePrincipalSupported = new BadRequestRegistration("Account must be an Azure Service Principal Account.");
        static readonly OctopusJsonRegistration<AzureResourceGroupResource[]> Results = new OctopusJsonRegistration<AzureResourceGroupResource[]>();

        readonly IOctopusHttpClientFactory httpClientFactory;

        public AzureResourceGroupsListAction(ISystemLog systemLog, IOctopusHttpClientFactory httpClientFactory) : base(systemLog)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public string Method => "GET";
        public string Route => "resourceGroups";
        public string Description => "Lists the Resource Groups associated with an Azure account.";

        public async Task<IOctoResponseProvider> Respond(IOctoRequest request, string accountName, AccountDetails accountDetails)
        {
            if (accountDetails.AccountType != AccountTypes.AzureServicePrincipalAccountType)
                return OnlyServicePrincipalSupported.Response();

            var servicePrincipalAccount = (AzureServicePrincipalAccountDetails) accountDetails;

            var resourceGroups = await RetrieveResourceGroups(accountName, servicePrincipalAccount);
            return Results.Response(resourceGroups);
        }

        Task<AzureResourceGroupResource[]> RetrieveResourceGroups(string accountName, AzureServicePrincipalAccountDetails accountDetails)
        {

            return ThrowIfNotSuccess(async () =>
            {
                using (var armClient = accountDetails.CreateResourceManagementClient(httpClientFactory.HttpClientHandler))
                {
                    return await armClient.ResourceGroups.ListWithHttpMessagesAsync().ConfigureAwait(false);
                }
            }, response =>
            {
                return response.Body
                    .Select(x => new AzureResourceGroupResource {Id = x.Id, Name = x.Name}).ToArray();
            }, $"Failed to retrieve list of Resource Groups for '{accountName}' service principal.");
        }
    }
}