using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.Runtime;
using Calamari.Aws.Discovery;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.ServiceMessages;
using Octopus.Calamari.Contracts.TargetDiscovery;
using Octopus.CoreUtilities.Extensions;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Aws.Behaviours;

public class EcsClusterDiscoveryBehaviour(IEcsClientFactory ecsClientFactory, ILog log) : IDeployBehaviour
{
    // DescribeClusters accepts at most 100 cluster identifiers per request.
    const int DescribeClustersBatchSize = 100;

    public bool IsEnabled(RunningDeployment context) => true;

    public async Task Execute(RunningDeployment deployment)
    {
        const string contextVariableName = TargetDiscoverySpecialVariables.TargetDiscoveryContext;

        var contextJson = deployment.Variables.Get(contextVariableName);
        if (string.IsNullOrEmpty(contextJson))
        {
            log.Warn($"Could not find target discovery context in variable {contextVariableName}.");
            log.Warn("Aborting target discovery.");
            return;
        }

        if (!AwsTargetDiscoveryContextResolver.TryResolve(contextJson, log, out var discoveryContext))
        {
            log.Warn("Aborting target discovery.");
            return;
        }

        var authentication = discoveryContext.Authentication;
        var scope = discoveryContext.Scope;

        LogAuthenticationDetails(authentication);

        if (!authentication.TryGetCredentials(log, out var credentials))
        {
            log.Warn("Aborting target discovery.");
            return;
        }

        var discoveredTargetCount = 0;
        try
        {
            foreach (var region in authentication.Regions)
            {
                using var client = ecsClientFactory.Create(credentials, region);

                foreach (var cluster in await DiscoverClustersInRegion(client, region))
                {
                    var tags = (cluster.Tags ?? new List<Tag>())
                               .Select(tag => new KeyValuePair<string, string>(tag.Key, tag.Value))
                               .ToTargetTags();

                    var matchResult = scope.Match(tags);
                    if (matchResult.IsSuccess)
                    {
                        discoveredTargetCount++;
                        log.Info($"Discovered matching ECS cluster '{cluster.ClusterName}' in {region}.");
                        WriteTargetCreationServiceMessage(region, cluster, authentication, scope, matchResult);
                    }
                    else
                    {
                        log.Verbose($"ECS cluster '{cluster.ClusterName}' in {region} does not match target requirements:");
                        foreach (var reason in matchResult.FailureReasons)
                        {
                            log.Verbose($"- {reason}");
                        }
                    }
                }
            }
        }
        catch (AmazonServiceException ex)
        {
            log.Warn("Error connecting to AWS to look for ECS clusters:");
            log.Warn(ex.Message);
            log.Warn("Aborting target discovery.");
            return;
        }

        log.Info(discoveredTargetCount > 0
                     ? $"{discoveredTargetCount} ECS cluster target{(discoveredTargetCount > 1 ? "s" : "")} found."
                     : "Could not find any ECS cluster targets.");
    }

    async Task<IReadOnlyList<Cluster>> DiscoverClustersInRegion(IAmazonECS client, string region)
    {
        log.Verbose($"Listing ECS clusters in region {region}.");

        var clusterArns = new List<string>();
        string nextToken = null;
        do
        {
            var response = await client.ListClustersAsync(new ListClustersRequest { NextToken = nextToken });
            clusterArns.AddRange(response.ClusterArns ?? Enumerable.Empty<string>());
            nextToken = response.NextToken;
        } while (!string.IsNullOrEmpty(nextToken));

        var clusters = new List<Cluster>();
        foreach (var batch in clusterArns.Chunk(DescribeClustersBatchSize))
        {
            var response = await client.DescribeClustersAsync(new DescribeClustersRequest
            {
                Clusters = batch.ToList(),
                // Tags aren't returned by default and are required to match the discovery scope.
                Include = [ClusterField.TAGS]
            });

            clusters.AddRange((response.Clusters ?? Enumerable.Empty<Cluster>())
                              .Where(cluster => cluster.Status != "INACTIVE"));
        }

        log.Verbose($"Found {clusters.Count} active ECS cluster(s) in region {region}.");
        return clusters;
    }

    void LogAuthenticationDetails(IAwsAuthenticationDetails authentication)
    {
        log.Verbose("Looking for ECS clusters in AWS using:");
        log.Verbose($"\tAccount: {authentication.AccountId}");
        log.Verbose($"\tRegions: [{string.Join(",", authentication.Regions)}]");

        if (authentication.Role.Type == "assumeRole")
        {
            log.Verbose("\tRole:");
            log.Verbose($"\t\tARN: {authentication.Role.Arn}");
            if (!authentication.Role.SessionName.IsNullOrEmpty())
            {
                log.Verbose($"\t\tSession Name: {authentication.Role.SessionName}");
            }
            if (authentication.Role.SessionDuration != null)
            {
                log.Verbose($"\t\tSession Duration: {authentication.Role.SessionDuration}");
            }
            if (!authentication.Role.ExternalId.IsNullOrEmpty())
            {
                log.Verbose($"\t\tExternal Id: {authentication.Role.ExternalId}");
            }
        }
        else
        {
            log.Verbose("\tRole: No IAM Role provided.");
        }
    }

    void WriteTargetCreationServiceMessage(
        string region,
        Cluster cluster,
        IAwsAuthenticationDetails authentication,
        TargetDiscoveryScope scope,
        TargetMatchResult matchResult)
    {
        var role = authentication.Role;
        var assumesRole = role?.Type == "assumeRole";

        var useInstanceRole = authentication is AwsWorkerAuthenticationDetails;

        var properties = new Dictionary<string, string>
        {
            // Generic create-target attributes consumed by the server's target-creation framework.
            { DefaultKeyNames.Name, $"aws-ecs/{region}/{cluster.ClusterName}" },
            { DefaultKeyNames.OctopusRoles, matchResult.Role },
            { DefaultKeyNames.UpdateIfExisting, bool.TrueString },
            { DefaultKeyNames.IsDynamic, bool.TrueString },
            { DefaultKeyNames.TenantedDeploymentParticipation, matchResult.TenantedDeploymentMode },

            // ECS-cluster-specific attributes (see server-side AwsEcsClusterMessageHandler).
            { AwsEcsServiceMessageNames.AccountIdOrNameAttribute, authentication.AccountId },
            { AwsEcsServiceMessageNames.ClusterNameAttribute, cluster.ClusterName },
            { AwsEcsServiceMessageNames.WorkerPoolIdOrNameAttribute, scope.WorkerPoolId },
            { AwsEcsServiceMessageNames.ClusterRegionAttribute, region },
            { AwsEcsServiceMessageNames.UseInstanceRole, useInstanceRole.ToString() },
            { AwsEcsServiceMessageNames.AssumeRole, assumesRole.ToString() },
            { AwsEcsServiceMessageNames.AssumeRoleArn, assumesRole ? role.Arn : null },
            { AwsEcsServiceMessageNames.AssumeRoleSession, assumesRole ? role.SessionName : null },
            { AwsEcsServiceMessageNames.AssumeRoleSessionDurationSeconds, assumesRole ? role.SessionDuration?.ToString() : null },
            { AwsEcsServiceMessageNames.AssumeRoleExternalId, assumesRole ? role.ExternalId : null },
        };

        var serviceMessage = new ServiceMessage(
            AwsEcsServiceMessageNames.CreateTargetName,
            properties.Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value));

        log.WriteServiceMessage(serviceMessage);
    }

    /// <summary>
    /// The service message name and attribute keys used by server.
    /// Note: Should these migrate to CalamariContracts?
    /// </summary>
    static class AwsEcsServiceMessageNames
    {
        public const string CreateTargetName = "create-aws-ecs-target";
        public const string AccountIdOrNameAttribute = "octopusAccountIdOrName";
        public const string ClusterNameAttribute = "clusterName";
        public const string WorkerPoolIdOrNameAttribute = "octopusDefaultWorkerPoolIdOrName";
        public const string ClusterRegionAttribute = "clusterRegion";
        public const string UseInstanceRole = "useInstanceRole";
        public const string AssumeRole = "assumeRole";
        public const string AssumeRoleArn = "assumeRoleArn";
        public const string AssumeRoleSession = "assumeRoleSession";
        public const string AssumeRoleSessionDurationSeconds = "assumeRoleSessionDurationSeconds";
        public const string AssumeRoleExternalId = "assumeRoleExternalId";
    }
}
