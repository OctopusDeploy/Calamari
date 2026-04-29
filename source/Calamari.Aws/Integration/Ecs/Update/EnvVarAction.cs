using System.Collections.Generic;

namespace Calamari.Aws.Integration.Ecs.Update;

public enum EnvVarMode { None, Replace, Append }

public enum EnvVarKind { Plain, Secret }

public record EnvVarItem(string Name, string Value, EnvVarKind Kind);

public record EnvVarAction(EnvVarMode Mode, IReadOnlyList<EnvVarItem> Items)
{
    public static EnvVarAction None { get; } = new(EnvVarMode.None, []);
}
