using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
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
