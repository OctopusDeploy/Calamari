using System;
using Calamari.ArgoCD.Git;

#if NET

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models
{
    public record HelmValueFileReference(string Path, string FullReference, IGitConnection GitConnection);    
}
#endif
