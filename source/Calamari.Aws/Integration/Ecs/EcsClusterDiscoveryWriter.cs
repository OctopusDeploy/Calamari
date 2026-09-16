using System.Collections.Generic;
using System.Linq;
using Amazon.ECS.Model;
using Calamari.Aws.Discovery;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Octopus.Calamari.Contracts.TargetDiscovery;

namespace Calamari.Aws.Integration.Ecs;

public interface IEcsClusterDiscoveryWriter
{
    void WriteTargetCreationServiceMessage(
        string region,
        Cluster cluster,
        IAwsAuthenticationDetails authentication,
        TargetDiscoveryScope scope,
        TargetMatchResult matchResult);
}

public class EcsClusterDiscoveryWriter(ILog log) : IEcsClusterDiscoveryWriter
{
    public void WriteTargetCreationServiceMessage(string region,
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
            properties.Where(p => p.Value != null)
                      .ToDictionary(p => p.Key, p => p.Value));

        log.WriteServiceMessage(serviceMessage);
    }
}