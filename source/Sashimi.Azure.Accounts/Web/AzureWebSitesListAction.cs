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
    class AzureWebSitesListAction : AzureActionBase, IAccountDetailsEndpoint
    {
        static readonly BadRequestRegistration OnlyServicePrincipalSupported = new("Account must be an Azure Service Principal Account.");
        static readonly OctopusJsonRegistration<ICollection<AzureWebSiteResource>> Results = new();

        readonly IOctopusHttpClientFactory httpClientFactory;

        public AzureWebSitesListAction(ISystemLog systemLog, IOctopusHttpClientFactory httpClientFactory)
            : base(systemLog)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public string Method => "GET";
        public string Route => "websites";
        public string Description => "Lists the websites associated with an Azure account.";

        public async Task<IOctoResponseProvider> Respond(IOctoRequest request, string accountName, AccountDetails accountDetails)
        {
            if (accountDetails.AccountType != AccountTypes.AzureServicePrincipalAccountType)
                return OnlyServicePrincipalSupported.Response();

            var sites = (await GetSites(accountName, (AzureServicePrincipalAccountDetails)accountDetails))
                        .OrderBy(x => x.Name)
                        .ThenBy(x => x.Region)
                        .ToArray();
            return Results.Response(sites);
        }

        Task<List<AzureWebSiteResource>> GetSites(string accountName, AzureServicePrincipalAccountDetails accountDetails)
        {
            return ThrowIfNotSuccess(async () =>
                                     {
                                         using (var webSiteClient = accountDetails.CreateWebSiteManagementClient(httpClientFactory.HttpClientHandler))
                                         {
                                             return await webSiteClient.WebApps.ListWithHttpMessagesAsync().ConfigureAwait(false);
                                         }
                                     },
                                     response => response.Body
                                                         .Select(site => AzureWebSiteResource.ForResourceManagement(site.Name, site.ResourceGroup, site.Location))
                                                         .ToList(),
                                     $"Failed to retrieve list of WebApps for '{accountName}' service principal.");
        }
    }
}