using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs.Deploy.Cfn;

public sealed record TaskDefinitionProperties
{
    public ContainerDefinition[] ContainerDefinitions { get; init; }
    public Value<string> Family { get; init; }
    public Value<string> Cpu { get; init; }
    public Value<string> Memory { get; init; }
    public Value<string> ExecutionRoleArn { get; init; }
    public Value<string> TaskRoleArn { get; init; }
    public string[] RequiresCompatibilities { get; init; }
    public string NetworkMode { get; init; }
    public RuntimePlatform RuntimePlatform { get; init; }
    public Volume[] Volumes { get; init; }
    public Tag[] Tags { get; init; }
}

public sealed record RuntimePlatform
{
    public string OperatingSystemFamily { get; init; }
    public string CpuArchitecture { get; init; }
}

public sealed record Volume
{
    public string Name { get; init; }
    public EfsVolumeConfiguration EFSVolumeConfiguration { get; init; }
}

public sealed record EfsVolumeConfiguration
{
    public string FilesystemId { get; init; }
    public string RootDirectory { get; init; }
    public string TransitEncryption { get; init; }
    public AuthorizationConfig AuthorizationConfig { get; init; }
}

public sealed record AuthorizationConfig
{
    // CFN's actual property name is IAM (all-caps); keep the C# property in
    // conventional PascalCase and override the JSON name here.
    [JsonProperty("IAM")]
    public string Iam { get; init; }
    public string AccessPointId { get; init; }
}
