namespace Calamari.Aws.Integration.Ecs.Deploy.Cfn;

public sealed record IamRoleProperties
{
    public string Path { get; init; }
    public Value<string>[] ManagedPolicyArns { get; init; }
    public AssumeRolePolicyDocument AssumeRolePolicyDocument { get; init; }
}

public sealed record AssumeRolePolicyDocument
{
    public string Version { get; init; }
    public AssumeRoleStatement[] Statement { get; init; }
}

public sealed record AssumeRoleStatement
{
    public string Effect { get; init; }
    public AssumeRolePrincipal Principal { get; init; }
    public string[] Action { get; init; }
}

public sealed record AssumeRolePrincipal
{
    public string[] Service { get; init; }
}
