namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record TypedKeyValuePair
{
    public KeyValueType Type { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public enum KeyValueType
{
    Plain,
    Secret
}
