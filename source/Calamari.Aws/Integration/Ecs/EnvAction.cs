using System.Collections.Generic;

namespace Calamari.Aws.Integration.Ecs;

public enum EnvActionMode { Replace, Merge }

public record EnvAction<T>(EnvActionMode Action, IReadOnlyList<T> Items);
