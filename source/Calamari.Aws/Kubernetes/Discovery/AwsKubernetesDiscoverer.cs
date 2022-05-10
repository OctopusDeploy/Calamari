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
            if (!TryGetAuthenticationDetails(contextJson, out var authenticationDetails))
                yield break;
            var workerPool = variables.Get("Octopus.Aws.WorkerPool");
            
            var accessKeyOrWorkerCredentials = authenticationDetails.Credentials.Type == "account"
                ? authenticationDetails.Credentials.Account.AccessKey
                : $"Using Worker Credentials on Worker Pool: {workerPool}";

            log.Verbose("Looking for Kubernetes clusters in AWS using:");
            log.Verbose($"  Regions: [{string.Join(',',authenticationDetails.Regions)}]");
            log.Verbose($"  Account: {accessKeyOrWorkerCredentials}");
            if (authenticationDetails.Role.Type == "assumeRole")
            {
                log.Verbose($"  Role: {authenticationDetails.Role.Arn}");
                if (!authenticationDetails.Role.SessionName.IsNullOrEmpty())
                    log.Verbose($"    Session Name: {authenticationDetails.Role.SessionName}");
                if (authenticationDetails.Role.SessionDuration is {} sessionDuration) 
                    log.Verbose($"    Session Duration: {sessionDuration}");
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
                
                log.Info("Discovery has discovered");

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
                        workerPool,
                        cluster.Tags.ToTargetTags());
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