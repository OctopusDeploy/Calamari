using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    class EnvironmentId : CaseSensitiveStringTinyType
    {
        public EnvironmentId(string value) : base(value)
        {
        }
    }
}