using System;

#if NET

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models
{
    public record HelmValueFileReference(string Path, string FullReference, ArgoCDRefApplicationSource RefSource);    
}
#endif
