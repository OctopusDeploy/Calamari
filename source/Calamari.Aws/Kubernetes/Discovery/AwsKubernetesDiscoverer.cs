using System;
using System.Collections.Generic;
using Amazon;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Aws;
using Newtonsoft.Json;

namespace Calamari.Aws.Kubernetes.Discovery
{
    public class AwsKubernetesDiscoverer : IKubernetesDiscoverer
    {
        readonly ILog log;

        public AwsKubernetesDiscoverer(ILog log)
        {
            this.log = log;
        }

        public string Name => "Aws";
        
        public IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson)
        {
            if (!TryGetAuthenticationDetails(contextJson, out var authenticationDetails))
                yield break;
            
            // var account = authenticationDetails.AccountDetails;
            // log.Verbose("Looking for Kubernetes clusters in Azure using:");
            // log.Verbose($"  Subscription ID: {account.SubscriptionNumber}");
            // log.Verbose($"  Tenant ID: {account.TenantId}");
            // log.Verbose($"  Client ID: {account.ClientId}");
            // var azureClient = account.CreateAzureClient();
            //
            // return azureClient.KubernetesClusters.List()
            //                   .Select(c => new KubernetesCluster(c.Name,
            //                       c.ResourceGroupName,
            //                       authenticationDetails.AccountId,
            //                       c.Tags.ToTargetTags()));
            
            foreach (var region in authenticationDetails.Regions)
            {
                var client = new AmazonEKSClient(authenticationDetails.ToCredentials(),
                    RegionEndpoint.GetBySystemName(region));

                var clusters = client.ListClustersAsync(new ListClustersRequest()).GetAwaiter().GetResult();
                
                log.Info("Discovery has discovered");

                foreach (var cluster in clusters.Clusters.Select(c =>
                    client.DescribeClusterAsync(new DescribeClusterRequest { Name = c }).GetAwaiter().GetResult().Cluster))
                {
                    yield return cluster;
                }
            }
        }
        
        bool TryGetAuthenticationDetails(string json, 
            out AwsAuthenticationDetails authenticationDetails)
        {
            authenticationDetails = null;
            try
            {
                authenticationDetails =
                    JsonConvert.DeserializeObject<TargetDiscoveryContext<AwsAuthenticationDetails>>(json)
                               ?.Authentication;
                return authenticationDetails != null;
            }
            catch (Exception ex)
            {
                log.Warn($"Target discovery context is in wrong format: {ex.Message}");
                return false;
            }
        }
    }
}