using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Rest.Azure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Azure.Kubernetes.Discovery
{
    using AzureTargetDiscoveryContext = TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>;

    public class AzureKubernetesDiscoverer : KubernetesDiscovererBase
    {
        public AzureKubernetesDiscoverer(ILog log) : base(log)
        {
        }

        /// <remarks>
        /// This type value here must be the same as in Octopus.Server.Orchestration.ServerTasks.Deploy.TargetDiscovery.TargetDiscoveryAuthenticationDetailsFactory.AzureAuthenticationDetailsFactory
        /// This value is hardcoded because:
        /// a) There is currently no existing project to place code shared between server and Calamari, and
        /// b) We expect a bunch of stuff in the Sashimi/Calamari space to be refactored back into the OctopusDeploy solution soon.
        /// </remarks>
        public override string Type => "Azure";

        public override IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson)
        {
            var accountType = JToken.Parse(contextJson).SelectToken("Authentication").SelectToken("Type").ToString();

            IAzureAccount account;
            string accountId;
            if (accountType == "AzureOidc")
            {
                if (!TryGetDiscoveryContext<AccountAuthenticationDetails<AzureOidcAccount>>(contextJson, out var oidcAuthenticationDetails, out _))
                    return Enumerable.Empty<KubernetesCluster>();
                accountId = oidcAuthenticationDetails.AccountId;
                account = oidcAuthenticationDetails.AccountDetails;
            }
            else
            {
                if (!TryGetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>(contextJson, out var servicePrincipalAuthenticationDetails, out _))
                    return Enumerable.Empty<KubernetesCluster>();
                accountId = servicePrincipalAuthenticationDetails.AccountId;
                account = servicePrincipalAuthenticationDetails.AccountDetails;
            }

            Log.Verbose("Looking for Kubernetes clusters in Azure using:");
            Log.Verbose($"  Subscription ID: {account.SubscriptionNumber}");
            Log.Verbose($"  Tenant ID: {account.TenantId}");
            Log.Verbose($"  Client ID: {account.ClientId}");
            var azureClient = account.CreateAzureClient();

            var discoveredClusters = new List<KubernetesCluster>();

            // There appears to be an issue where the azure client returns stale data
            // We need to upgrade this to use the newer SDK, but we need to upgrade to .NET 4.6.2 to support that.
            var resourceGroups = azureClient.ResourceGroups.List();
            //we don't care about resource groups that are being deleted 
            foreach (var resourceGroup in resourceGroups.Where(rg => rg.ProvisioningState != "Deleting"))
            {
                try
                {
                    // There appears to be an issue where the azure client returns stale data
                    // to mitigate this, specifically for scenario's where the resource group doesn't exist anymore
                    // we specifically list the clusters in each resource group
                    var clusters = azureClient.KubernetesClusters.ListByResourceGroup(resourceGroup.Name);

                    discoveredClusters.AddRange(
                                                clusters
                                                    .Select(c => KubernetesCluster.CreateForAks(
                                                                                                $"aks/{account.SubscriptionNumber}/{c.ResourceGroupName}/{c.Name}",
                                                                                                c.Name,
                                                                                                c.ResourceGroupName,
                                                                                                accountId,
                                                                                                c.Tags.ToTargetTags())));
                }
                catch (CloudException ex)
                {
                    Log.Verbose($"Failed to list kubernetes clusters for resource group {resourceGroup.Name}. Response message: {ex.Message}, Status code: {ex.Response.StatusCode}");
                    
                    // if the resource group was not found, we don't care and move on
                    if (ex.Response.StatusCode == HttpStatusCode.NotFound && ex.Message.StartsWith("Resource group"))
                        continue;

                    //throw in all other scenario's
                    throw;
                }
            }

            return discoveredClusters;
        }
    }
}