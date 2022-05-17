using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Azure.Kubernetes.Discovery
{
    using AzureTargetDiscoveryContext = TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>;
    
    public class AzureKubernetesDiscoverer : IKubernetesDiscoverer
    {
        readonly ILog log;

        public AzureKubernetesDiscoverer(ILog log)
        {
            this.log = log;
        }

        public string Name => KubernetesAuthenticationContextTypes.AzureServicePrincipal;

        public IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson)
        {
            if (!TryGetAuthenticationDetails(contextJson, out var authenticationDetails))
                return Enumerable.Empty<KubernetesCluster>();
            
            var account = authenticationDetails.AccountDetails;
            log.Verbose("Looking for Kubernetes clusters in Azure using:");
            log.Verbose($"  Subscription ID: {account.SubscriptionNumber}");
            log.Verbose($"  Tenant ID: {account.TenantId}");
            log.Verbose($"  Client ID: {account.ClientId}");
            var azureClient = account.CreateAzureClient();

            return azureClient.KubernetesClusters.List()
                              .Select(c => KubernetesCluster.CreateForAks(c.Name,
                                  c.ResourceGroupName,
                                  authenticationDetails.AccountId,
                                  c.Tags.ToTargetTags()));
        }

        bool TryGetAuthenticationDetails(string contextJson, 
            out AccountAuthenticationDetails<ServicePrincipalAccount> authenticationDetails)
        {
            authenticationDetails = null;
            try
            {
                authenticationDetails = 
                    JsonConvert.DeserializeObject<AzureTargetDiscoveryContext>(contextJson)
                               ?.Authentication;
                
                if (authenticationDetails != null)
                    return true;
                
                log.Warn("Target discovery context is in the wrong format.");
                return false;
            }
            catch (JsonException ex)
            {
                log.Warn($"Target discovery context is in the wrong format: {ex.Message}");
                return false;
            }
        }
    }
}