using System.Collections.Generic;

namespace Calamari.Aws.Integration.Ecs.Update;

public enum EnvFileMode { None, Replace, Append }

// Only the s3 file type is supported (matches SPF).
public record EnvFileItem(string Value);

public record EnvFileAction(EnvFileMode Mode, IReadOnlyList<EnvFileItem> Items)
{
    public static EnvFileAction None { get; } = new(EnvFileMode.None, []);
}
