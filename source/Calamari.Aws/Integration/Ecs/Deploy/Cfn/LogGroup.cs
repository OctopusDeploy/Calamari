namespace Calamari.Aws.Integration.Ecs.Deploy.Cfn;

public sealed record LogGroupProperties
{
    public Value<string> LogGroupName { get; init; }
}
