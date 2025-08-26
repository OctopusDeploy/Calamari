#if NET
using System;
using Calamari.ArgoCD.Git;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{
    public record ArgoCDImageUpdateTarget(
        string DefaultClusterRegistry,
        IGitConnection gitConnection);    
    
    public ArgoCDImageUpdateTarget FromVariables(IVariables variables)
    {
        return new ArgoCDImageUpdateTarget(
                                           
                                           )
    };
}
#endif