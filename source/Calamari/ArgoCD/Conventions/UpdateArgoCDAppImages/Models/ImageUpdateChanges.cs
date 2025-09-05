#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models
{
    public record ImageUpdateChanges(HashSet<string> updatedFiles, HashSet<string> UpdatedImageReferences);    
}

#endif