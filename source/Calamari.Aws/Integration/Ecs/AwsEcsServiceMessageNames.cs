namespace Calamari.Aws.Integration.Ecs;

/// <summary>
/// The service message name and attribute keys used by server.
/// Note: Should these migrate to CalamariContracts?
/// </summary>
public static class AwsEcsServiceMessageNames
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