#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models
{
    public record ImageUpdateChanges(Dictionary<string, string> UpdatedFiles, HashSet<string> UpdatedImageReferences);    
}

#endif