using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    class ProjectId : CaseSensitiveStringTinyType
    {
        public ProjectId(string value) : base(value)
        {
        }
    }
}