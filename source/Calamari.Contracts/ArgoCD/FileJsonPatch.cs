using System;

namespace Octopus.Calamari.Contracts.ArgoCD;

public record FileJsonPatch(string FilePath, string JsonPatch);