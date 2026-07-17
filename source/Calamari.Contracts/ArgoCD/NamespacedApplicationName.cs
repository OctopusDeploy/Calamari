using System;
using Octopus.TinyTypes;

namespace Octopus.Calamari.Contracts.ArgoCD
{
    public class NamespacedApplicationName : CaseInsensitiveStringTinyType
    {
        NamespacedApplicationName(string value) : base(value)
        {
        }

        public static NamespacedApplicationName Create(string name, string kubernetesNamespace) => new($"{kubernetesNamespace}/{name}");
    }
}
