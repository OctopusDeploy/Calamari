using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.EKS;
using Amazon.EKS.Model;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Aws;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;

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
        
        public IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson, IVariables variables)
        {
            if (!TryGetDiscoveryContext(contextJson, out var discoveryContext))
                yield break;

            var authenticationDetails = discoveryContext.Authentication;
            if (authenticationDetails == null)
            {
                Log.Warn("Target Discovery Context is in the wrong format: Unable to serialise authentication details");
                yield break;
            }
            
            var accessKeyOrWorkerCredentials = authenticationDetails.Credentials.Type == "account"
                ? $"Access Key: {authenticationDetails.Credentials.Account.AccessKey}"
                : $"Using Worker Credentials on Worker Pool: {discoveryContext.Scope.WorkerPoolId}";

            log.Verbose("Looking for Kubernetes clusters in AWS using:");
            log.Verbose($"  Regions: [{string.Join(",",authenticationDetails.Regions)}]");
            
            log.Verbose("  Account:");
            log.Verbose($"    {accessKeyOrWorkerCredentials}");
            
            if (authenticationDetails.Role.Type == "assumeRole")
            {
                log.Verbose("  Role:");
                log.Verbose($"    ARN: {authenticationDetails.Role.Arn}");
                if (!authenticationDetails.Role.SessionName.IsNullOrEmpty())
                    log.Verbose($"    Session Name: {authenticationDetails.Role.SessionName}");
                if (authenticationDetails.Role.SessionDuration != null) 
                    log.Verbose($"    Session Duration: {authenticationDetails.Role.SessionDuration}");
                if (!authenticationDetails.Role.ExternalId.IsNullOrEmpty())
                    log.Verbose($"    External Id: {authenticationDetails.Role.ExternalId}");
            }
            else
            {
                log.Verbose("  Role: No IAM Role provided.");
            }
            
            foreach (var region in authenticationDetails.Regions)
            {
                var client = new AmazonEKSClient(authenticationDetails.ToCredentials(),
                    RegionEndpoint.GetBySystemName(region));

                var clusters = client.ListClustersAsync(new ListClustersRequest()).GetAwaiter().GetResult();

                foreach (var cluster in clusters.Clusters.Select(c =>
                    client.DescribeClusterAsync(new DescribeClusterRequest { Name = c }).GetAwaiter().GetResult().Cluster))
                {
                    var credentialsRole = authenticationDetails.Role;
                    var assumedRole = credentialsRole.Type == "assumeRole"
                        ? new AwsAssumeRole(credentialsRole.Arn,
                            credentialsRole.SessionName,
                            credentialsRole.SessionDuration,
                            credentialsRole.ExternalId)
                        : null;
                    
                    yield return KubernetesCluster.CreateForEks(cluster.Arn,
                        cluster.Name,
                        cluster.Endpoint,
                        authenticationDetails.Credentials.AccountId,
                        assumedRole,
                        discoveryContext.Scope.WorkerPoolId,
                        cluster.Tags.ToTargetTags());
                }
            }
        }
        
        bool TryGetDiscoveryContext(string json, 
            out TargetDiscoveryContext<AwsAuthenticationDetails> discoveryContext)
        {
            discoveryContext = null;
            try
            {
                discoveryContext =
                    JsonConvert.DeserializeObject<TargetDiscoveryContext<AwsAuthenticationDetails>>(json);
                
                if (discoveryContext != null)
                    return true;
                
                log.Warn("Target discovery context is in the wrong format: unable to serialise Target Discovery Context");
                return false;
            }
            catch (Exception ex)
            {
                log.Warn($"Target discovery context is in wrong format: {ex.Message}");
                return false;
            }
        }
    }
}