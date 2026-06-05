using System;

namespace Octopus.Calamari.Contracts.ArgoCD
{
    public record QualifiedApplicationName(string Name, string KubernetesNamespace)
    {
        public string ToDisplayName()
        {
            return $"{KubernetesNamespace}/{Name}";
        }
    }
}
