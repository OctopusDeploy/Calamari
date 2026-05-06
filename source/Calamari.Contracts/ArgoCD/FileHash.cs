using System;

namespace Octopus.Calamari.Contracts.ArgoCD;

public record FileHash(string FilePath, string Hash);