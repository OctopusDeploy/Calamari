using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs.Deploy.Cfn;

public sealed record ServiceProperties
{
    public Value<string> Cluster { get; init; }
    public string LaunchType { get; init; }
    public Value<string> TaskDefinition { get; init; }
    public Value<int> DesiredCount { get; init; }

    // CFN's actual property name is EnableECSManagedTags (all-caps "ECS"); keep
    // the C# property in conventional PascalCase and override the JSON name here.
    [JsonProperty("EnableECSManagedTags")]
    public bool EnableEcsManagedTags { get; init; }

    public DeploymentConfiguration DeploymentConfiguration { get; init; }
    public NetworkConfiguration NetworkConfiguration { get; init; }
    public LoadBalancer[] LoadBalancers { get; init; }
    public Tag[] Tags { get; init; }
}

public sealed record DeploymentConfiguration
{
    public Value<int> MinimumHealthyPercent { get; init; }
    public Value<int> MaximumPercent { get; init; }
}

public sealed record NetworkConfiguration
{
    public AwsvpcConfiguration AwsvpcConfiguration { get; init; }
}

public sealed record AwsvpcConfiguration
{
    public string AssignPublicIp { get; init; }
    public string[] Subnets { get; init; }
    public string[] SecurityGroups { get; init; }
}

public sealed record LoadBalancer
{
    public string ContainerName { get; init; }
    public double? ContainerPort { get; init; }
    public string TargetGroupArn { get; init; }
}
