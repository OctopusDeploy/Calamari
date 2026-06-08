using System;
using Octopus.TinyTypes;

namespace Octopus.Calamari.Contracts.ArgoCD
{
    public class QualifiedApplicationName : CaseInsensitiveStringTinyType
    {
        QualifiedApplicationName(string value) : base(value)
        {
        }

        public static QualifiedApplicationName Create(string name, string kubernetesNamespace)
            => new QualifiedApplicationName($"{kubernetesNamespace}/{name}");
    }
}
