#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models
{
    public record ImageReplacementResult(string UpdatedContents, HashSet<string> UpdatedImageReferences);    
}
#endif