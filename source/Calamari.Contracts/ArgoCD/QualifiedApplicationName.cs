using System;

namespace Octopus.Calamari.Contracts.ArgoCD
{
    public class QualifiedApplicationName : CaseSensitiveStringTinyType
    {
        QualifiedApplicationName(string value) : base(value)
        {
        }

        public static QualifiedApplicationName Create(string name, string @namespace)
            => new QualifiedApplicationName($"{@namespace}/{name}");
    }
}
