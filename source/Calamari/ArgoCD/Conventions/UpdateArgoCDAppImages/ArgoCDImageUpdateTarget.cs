#if NET
using System;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{
    public record ArgoCDImageUpdateTarget(
        string Name,
        string DefaultClusterRegistry,
        string Path,
        Uri RepoUrl,
        string TargetRevision);    
}
#endif