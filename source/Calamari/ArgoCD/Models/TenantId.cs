using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    class TenantId : CaseSensitiveStringTinyType
    {
        public TenantId(string value) : base(value)
        {
        }
    }
}