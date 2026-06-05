using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    public record QualifiedApplicationName(string Name, string KubernetesNamespace)
    {
        public string ToDisplayName()
        {
            return $"{KubernetesNamespace}/{Name}";
        }
    }
}
