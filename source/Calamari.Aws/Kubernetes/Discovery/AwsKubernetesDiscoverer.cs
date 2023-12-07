using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.EKS;
using Amazon.EKS.Model;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Kubernetes.Discovery
{
    public class AwsKubernetesDiscoverer : KubernetesDiscovererBase
    {
        public AwsKubernetesDiscoverer(ILog log) : base(log)
        {
        }

        /// <remarks>
        /// This type value here must be the same as in Octopus.Server.Orchestration.ServerTasks.Deploy.TargetDiscovery.AwsAuthenticationContext
        /// This value is hardcoded because:
        /// a) There is currently no existing project to place code shared between server and Calamari, and
        /// b) We expect a bunch of stuff in the Sashimi/Calamari space to be refactored back into the OctopusDeploy solution soon.
        /// </remarks>
        public override string Type => "Aws";

        public override IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson)
        {
            if (!TryGetAwsCredentialsType(contextJson, out var credentialsType))
                yield break;

            if (!TryGetAwsAuthenticationDetails(
                    contextJson,
                    credentialsType,
                    out var workerPoolId, 
                    out var accountId,
                    out var roleArnOrAccessKeyOrWorkerCredentials,
                    out var authenticationDetails)) 
                yield break;

            Log.Verbose("Looking for Kubernetes clusters in AWS using:");
            Log.Verbose($"  Regions: [{string.Join(",",authenticationDetails.Regions)}]");

            Log.Verbose("  Account:");
            Log.Verbose($"    {roleArnOrAccessKeyOrWorkerCredentials}");

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
                        accountId,
                        assumedRole,
                        workerPoolId,
                        cluster.Tags.ToTargetTags());
                }
            }
        }

        private bool TryGetAwsCredentialsType(string contextJson, out string credentialsType)
        {
            try
            {
                var targetDiscoveryContext = JsonConvert.DeserializeObject<JObject>(contextJson);
                credentialsType = targetDiscoveryContext
                    .GetValue("Authentication", StringComparison.OrdinalIgnoreCase).Value<JObject>()
                    .GetValue("Credentials", StringComparison.OrdinalIgnoreCase).Value<JObject>()
                    .GetValue("Type", StringComparison.OrdinalIgnoreCase).Value<string>() ?? throw new Exception("Credentials type is null");
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("Could not read authentication method of target discovery context.");
                credentialsType = null;
                return false;
            }
        }

        private bool TryGetAwsAuthenticationDetails(
            string contextJson,
            string credentialsType,
            out string workerPoolId,
            out string accountId,
            out string roleArnOrAccessKeyOrWorkerCredentials,
            out IAwsAuthenticationDetails awsAuthenticationDetails)
        {
            accountId = null;
            roleArnOrAccessKeyOrWorkerCredentials = null;
            awsAuthenticationDetails = null;
            if (credentialsType == "worker")
            {
                if (!TryGetDiscoveryContext<AwsWorkerAuthenticationDetails>(contextJson, out var awsWorkerAuthenticationDetails, out workerPoolId))
                    return false;
                
                roleArnOrAccessKeyOrWorkerCredentials = $"Using Worker Credentials on Worker Pool: {workerPoolId}";

                accountId = awsWorkerAuthenticationDetails.Credentials.AccountId;
                awsAuthenticationDetails = awsWorkerAuthenticationDetails;

            }
            else
            {
                switch (credentialsType)
                {
                    case "account":
                    {
                        if (!TryGetDiscoveryContext<AwsAccessKeyAuthenticationDetails>(
                                contextJson,
                                out var awsAccessKeyAuthentication,
                                out workerPoolId))
                            return false;

                        roleArnOrAccessKeyOrWorkerCredentials =
                            $"Access Key: {awsAccessKeyAuthentication.Credentials.Account.AccessKey}";

                        accountId = awsAccessKeyAuthentication.Credentials.AccountId;
                        awsAuthenticationDetails = awsAccessKeyAuthentication;
                        break;
                    }
                    case "oidcAccount":
                    {
                        if (!TryGetDiscoveryContext<AwsOidcAuthenticationDetails>(
                                contextJson,
                                out var awsOidcAuthentication,
                                out workerPoolId))
                            return false;

                        roleArnOrAccessKeyOrWorkerCredentials =
                            $"Role ARN: {awsOidcAuthentication.Credentials.Account.RoleArn}";

                        accountId = awsOidcAuthentication.Credentials.AccountId;
                        awsAuthenticationDetails = awsOidcAuthentication;

                        break;
                    }
                    default:
                        throw new Exception("Unknown AWS account");
                }
            }

            return true;
        }
    }
}