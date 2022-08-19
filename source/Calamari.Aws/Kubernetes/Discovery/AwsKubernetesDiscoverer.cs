using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.EKS;
using Amazon.EKS.Model;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Kubernetes.Discovery
{
    public class AwsKubernetesDiscoverer : KubernetesDiscovererBase
    {
        public AwsKubernetesDiscoverer(ILog log) : base(log)
        {
        }

        public override string Type => "Aws";

        public override IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson)
        {
            if (!TryGetDiscoveryContext<AwsAuthenticationDetails>(contextJson, out var authenticationDetails, out var workerPoolId))
                yield break;
            
            var accessKeyOrWorkerCredentials = authenticationDetails.Credentials.Type == "account"
                ? $"Access Key: {authenticationDetails.Credentials.Account.AccessKey}"
                : $"Using Worker Credentials on Worker Pool: {workerPoolId}";

            Log.Verbose("Looking for Kubernetes clusters in AWS using:");
            Log.Verbose($"  Regions: [{string.Join(",",authenticationDetails.Regions)}]");
            
            Log.Verbose("  Account:");
            Log.Verbose($"    {accessKeyOrWorkerCredentials}");
            
            if (authenticationDetails.Role.Type == "assumeRole")
            {
                Log.Verbose("  Role:");
                Log.Verbose($"    ARN: {authenticationDetails.Role.Arn}");
                if (!authenticationDetails.Role.SessionName.IsNullOrEmpty())
                    Log.Verbose($"    Session Name: {authenticationDetails.Role.SessionName}");
                if (authenticationDetails.Role.SessionDuration != null) 
                    Log.Verbose($"    Session Duration: {authenticationDetails.Role.SessionDuration}");
                if (!authenticationDetails.Role.ExternalId.IsNullOrEmpty())
                    Log.Verbose($"    External Id: {authenticationDetails.Role.ExternalId}");
            }
            else
            {
                Log.Verbose("  Role: No IAM Role provided.");
            }

            if (!authenticationDetails.TryGetCredentials(Log, out var credentials))
                yield break;
            
            foreach (var region in authenticationDetails.Regions)
            {
                var client = new AmazonEKSClient(credentials,
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
                        workerPoolId,
                        cluster.Tags.ToTargetTags());
                }
            }
        }
    }
}