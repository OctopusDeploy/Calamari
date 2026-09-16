using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Runtime;
using Calamari.Aws.Discovery;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Octopus.Calamari.Contracts.TargetDiscovery;
using Octopus.CoreUtilities.Extensions;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Aws.Behaviours;

public class EcsClusterDiscoveryBehaviour(IEcsDiscoverer ecsDiscoverer, IAwsTargetDiscoveryContextResolver contextResolver, IEcsClusterDiscoveryWriter clusterDiscoveryWriter, ILog log) : IDeployBehaviour
{
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

        if (!contextResolver.TryResolve(contextJson, log, out var discoveryContext))
        {
            log.Warn("Aborting target discovery. Could not resolve context.");
            return;
        }

        var authentication = discoveryContext.Authentication;
        var scope = discoveryContext.Scope;

        LogAuthenticationDetails(authentication);

        if (!authentication.TryGetCredentials(log, out var credentials))
        {
            log.Warn("Aborting target discovery. Invalid credentials.");
            return;
        }

        var discoveredTargetCount = 0;
        try
        {
            foreach (var region in authentication.Regions)
            {
                foreach (var cluster in await ecsDiscoverer.DiscoverClustersInRegion(credentials, region))
                {
                    var tags = (cluster.Tags ?? [])
                               .Select(tag => new KeyValuePair<string, string>(tag.Key, tag.Value))
                               .ToTargetTags();

                    var matchResult = scope.Match(tags);
                    if (matchResult.IsSuccess)
                    {
                        discoveredTargetCount++;
                        log.Info($"Discovered matching ECS cluster '{cluster.ClusterName}' in {region}.");
                        clusterDiscoveryWriter.WriteTargetCreationServiceMessage(region, cluster, authentication, scope, matchResult);
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
}
