using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Azure.Kubernetes.Discovery
{
    using AzureTargetDiscoveryContext = TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>;
    
    public class AzureKubernetesDiscoverer : KubernetesDiscovererBase
    {
        public AzureKubernetesDiscoverer(ILog log) : base(log)
        {
        }

        public override string Type => "Azure";

        public override IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson)
        {
            if (!TryGetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>(contextJson, out var authenticationDetails, out _))
                return Enumerable.Empty<KubernetesCluster>();
            
            var account = authenticationDetails.AccountDetails;
            Log.Verbose("Looking for Kubernetes clusters in Azure using:");
            Log.Verbose($"  Subscription ID: {account.SubscriptionNumber}");
            Log.Verbose($"  Tenant ID: {account.TenantId}");
            Log.Verbose($"  Client ID: {account.ClientId}");
            var azureClient = account.CreateAzureClient();
            
            return azureClient.KubernetesClusters.List()
                              .Select(c => KubernetesCluster.CreateForAks(
                                  $"aks/{account.SubscriptionNumber}/{c.ResourceGroupName}/{c.Name}",
                                  c.Name,
                                  c.ResourceGroupName,
                                  authenticationDetails.AccountId,
                                  c.Tags.ToTargetTags()));
        }
    }
}