using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Azure;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.Resources;
using Calamari.CloudAccounts;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;

namespace Calamari.Azure.Kubernetes.Discovery
{
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
            if (!TryGetAccountType(contextJson, out string accountType))
                return Enumerable.Empty<KubernetesCluster>();

            if (!TryGetAccountKubernetesClusters(contextJson,
                                                 accountType,
                                                 out var account,
                                                 out var accountId))
                return Enumerable.Empty<KubernetesCluster>();

            Log.Verbose("Looking for Kubernetes clusters in Azure using:");
            Log.Verbose($"  Subscription ID: {account.SubscriptionNumber}");
            Log.Verbose($"  Tenant ID: {account.TenantId}");
            Log.Verbose($"  Client ID: {account.ClientId}");

            var armClient = account.CreateArmClient();

            var discoveredClusters = new List<KubernetesCluster>();
            
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(account.SubscriptionNumber));
            
            var resourceGroups = subscriptionResource.GetResourceGroups().GetAll();//.GetAll("provisioningState ne 'Deleting'");
            
            //we don't care about resource groups that are being deleted
            foreach (var resourceGroupResource in resourceGroups.Where(rg => !string.Equals(rg.Data.ResourceGroupProvisioningState,"Deleting", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    // There appears to be an issue where the azure client returns stale data
                    // to mitigate this, specifically for scenario's where the resource group doesn't exist anymore
                    // we specifically list the clusters in each resource group
                    var clusters = resourceGroupResource.GetContainerServiceManagedClusters().GetAll();

                    
                    discoveredClusters.AddRange(
                                                clusters
                                                    .Select(c => KubernetesCluster.CreateForAks(
                                                                                                $"aks/{account.SubscriptionNumber}/{resourceGroupResource.Data.Name}/{c.Data.Name}",
                                                                                                c.Data.Name,
                                                                                                resourceGroupResource.Data.Name,
                                                                                                accountId,
                                                                                                c.Data.Tags.ToTargetTags())));
                }
                catch (RequestFailedException ex)
                {
                    Log.Verbose($"Failed to list kubernetes clusters for resource group {resourceGroupResource.Data.Name}. Response message: {ex.Message}, Status code: {ex.Status}");
                    
                    // if the resource group was not found, we don't care and move on
                    if (ex.Status == (int)HttpStatusCode.NotFound && ex.GetRawResponse()?.Content.ToString().StartsWith("Resource group") == true)
                        continue;

                    //throw in all other scenario's
                    throw;
                }
            }

            return discoveredClusters;
        }

        bool TryGetAccountKubernetesClusters(string contextJson,
                                             string accountType,
                                             out IAzureAccount account,
                                             out string accountId)
        {
            if (accountType == "AzureOidc")
            {
                if (!TryGetDiscoveryContext<AccountAuthenticationDetails<AzureOidcAccount>>(contextJson, out var oidcAuthenticationDetails, out _))
                {
                    account = null;
                    accountId = null;
                    return false;
                }

                accountId = oidcAuthenticationDetails.AccountId;
                account = oidcAuthenticationDetails.AccountDetails;
            }
            else
            {
                if (!TryGetDiscoveryContext<AccountAuthenticationDetails<AzureServicePrincipalAccount>>(contextJson, out var servicePrincipalAuthenticationDetails, out _))
                {
                    account = null;
                    accountId = null;
                    return false;
                }

                accountId = servicePrincipalAuthenticationDetails.AccountId;
                account = servicePrincipalAuthenticationDetails.AccountDetails;
            }

            return true;
        }

        bool TryGetAccountType(string contextJson, out string accountType)
        {
            try
            {
                var targetDiscoveryContext = JsonConvert.DeserializeObject<TargetDiscoveryContext<AccountAuthenticationDetails<dynamic>>>(contextJson);
                accountType = targetDiscoveryContext?.Authentication?.AuthenticationMethod ?? throw new Exception("AuthenticationMethod is null");
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("Could not read authentication method of target discovery context.");
                accountType = null;
                return false;
            }
        }
    }
}