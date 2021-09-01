using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.WebSites;
using Octopus.Diagnostics;
using Octopus.Extensibility.Actions.Sashimi;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Azure.Accounts.Web
{
    class AzureWebSitesSlotListAction : AzureActionBase, IAccountDetailsEndpoint
    {
        static readonly IResponderPathParameter<string> ResourceGroupName = new RequiredPathParameterProperty<string>("resourceGroupName", "Azure resource group name");
        static readonly IResponderPathParameter<string> WebsiteName = new RequiredPathParameterProperty<string>("webSiteName", "Website name");
        static readonly BadRequestRegistration OnlyServicePrincipalSupported = new("Account must be an Azure Service Principal Account.");
        static readonly OctopusJsonRegistration<ICollection<AzureWebSiteSlotResource>> Results = new();

        readonly IOctopusHttpClientFactory httpClientFactory;

        public AzureWebSitesSlotListAction(
            ISystemLog systemLog,
            IOctopusHttpClientFactory httpClientFactory) : base(systemLog)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public string Route => "{resourceGroupName}/websites/{webSiteName}/slots";
        public string Description => "Lists the slots associated with an Azure Web Site.";

        public string Method => "GET";

        public async Task<IOctoResponseProvider> Respond(IOctoRequest request, string accountName, AccountDetails accountDetails)
        {
            return await request
                .HandleAsync(ResourceGroupName,
                             WebsiteName,
                             async (resourceGroupName, websiteName) =>
                             {
                                 var targetSite = GetAzureTargetSite(websiteName, string.Empty);
                                 var siteName = targetSite.Site;

                                 if (accountDetails.AccountType != AccountTypes.AzureServicePrincipalAccountType)
                                     return OnlyServicePrincipalSupported.Response();

                                 var servicePrincipalAccount = (AzureServicePrincipalAccountDetails)accountDetails;

                                 using (var webSiteClient = servicePrincipalAccount.CreateWebSiteManagementClient(httpClientFactory.HttpClientHandler))
                                 {
                                     var slots = (await GetSlots(webSiteClient, resourceGroupName, siteName)).OrderBy(s => s.Name).ToArray();
                                     return Results.Response(slots);
                                 }
                             });
        }

        Task<List<AzureWebSiteSlotResource>> GetSlots(WebSiteManagementClient client, string resourceGroup, string siteName)
        {
            return ThrowIfNotSuccess(() => client.WebApps.ListSlotsWithHttpMessagesAsync(resourceGroup, siteName),
                                     response => response.Body
                                                         .Select(slot => new AzureWebSiteSlotResource { Name = GetAzureTargetSite(slot.Name, string.Empty)?.Slot, Site = slot.RepositorySiteName, ResourceGroupName = slot.ResourceGroup, Region = slot.Location })
                                                         .ToList(),
                                     $"Failed to retrieve list of slots for '{resourceGroup}' resource group in '{siteName}' site.");
        }

        static AzureTargetSite GetAzureTargetSite(string siteAndMaybeSlotName, string slotName)
        {
            AzureTargetSite targetSite = new()
                { RawSite = siteAndMaybeSlotName };

            if (siteAndMaybeSlotName.Contains("("))
            {
                // legacy site and slot "site(slot)"
                var parenthesesIndex = siteAndMaybeSlotName.IndexOf("(", StringComparison.Ordinal);
                targetSite.Site = siteAndMaybeSlotName.Substring(0, parenthesesIndex).Trim();
                targetSite.Slot = siteAndMaybeSlotName.Substring(parenthesesIndex + 1).Replace(")", string.Empty).Trim();
                return targetSite;
            }

            if (siteAndMaybeSlotName.Contains("/"))
            {
                // "site/slot"
                var slashIndex = siteAndMaybeSlotName.IndexOf("/", StringComparison.Ordinal);
                targetSite.Site = siteAndMaybeSlotName.Substring(0, slashIndex).Trim();
                targetSite.Slot = siteAndMaybeSlotName.Substring(slashIndex + 1).Trim();
                return targetSite;
            }

            targetSite.Site = siteAndMaybeSlotName;
            targetSite.Slot = slotName;
            return targetSite;
        }

        class AzureWebSiteSlotResource
        {
            public string? Name { get; set; }
            public string Site { get; set; } = null!;
            public string ResourceGroupName { get; set; } = null!;
            public string Region { get; set; } = null!;
        }

        public class AzureTargetSite
        {
            public string RawSite { get; set; } = null!;
            public string Site { get; set; } = null!;
            public string Slot { get; set; } = null!;

            public string SiteAndSlot => HasSlot ? $"{Site}/{Slot}" : Site;

            public bool HasSlot => !string.IsNullOrWhiteSpace(Slot);
        }
    }
}