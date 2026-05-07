namespace Calamari.Aws.Integration.Ecs;

public enum EnvVarType { Text, Secret }

public record EnvVarItem(EnvVarType Type, string Key, string Value);
