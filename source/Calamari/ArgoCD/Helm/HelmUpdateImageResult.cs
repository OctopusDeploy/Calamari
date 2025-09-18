#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm
{
    public record HelmRefUpdatedResult(Uri RepoUrl, HashSet<string> ImagesUpdated);    
}

#endif