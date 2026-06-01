using System.Collections.Generic;

namespace Calamari.Aws.Integration.Ecs.Deploy.Cfn;

public sealed record Template
{
    public string AWSTemplateFormatVersion { get; init; } = "2010-09-09";
    public Dictionary<string, ParameterDef> Parameters { get; init; } = new();
    public Dictionary<string, Resource> Resources { get; init; } = new();
}

public sealed record ParameterDef
{
    public string Type { get; init; }
    public object Default { get; init; }
}

// Single record for any CFN resource. Properties is typed as object so different
// resources can carry different strongly-typed property records — the static
// factory methods below enforce the right Properties type per resource kind.
public sealed record Resource
{
    public string Type { get; init; }
    public string DependsOn { get; init; }
    public object Properties { get; init; }

    public static Resource TaskDefinition(TaskDefinitionProperties properties) =>
        new() { Type = "AWS::ECS::TaskDefinition", Properties = properties };

    public static Resource Service(string dependsOn, ServiceProperties properties) =>
        new() { Type = "AWS::ECS::Service", DependsOn = dependsOn, Properties = properties };

    public static Resource LogGroup(LogGroupProperties properties) =>
        new() { Type = "AWS::Logs::LogGroup", Properties = properties };

    public static Resource IamRole(IamRoleProperties properties) =>
        new() { Type = "AWS::IAM::Role", Properties = properties };
}

public sealed record Tag
{
    public string Key { get; init; }
    public string Value { get; init; }
}
