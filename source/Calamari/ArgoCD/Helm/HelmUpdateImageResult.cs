#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Helm
{
    public record HelmRefUpdatedResult(Uri RepoUrl, HashSet<string> ImagesUpdated);    
}

#endif