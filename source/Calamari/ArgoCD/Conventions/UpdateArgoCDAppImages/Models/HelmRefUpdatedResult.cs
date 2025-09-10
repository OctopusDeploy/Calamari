#if NET
using System;
using System.Collections.Generic;


namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models
{
    public record HelmRefUpdatedResult(string RepoUrl, HashSet<string> ImagesUpdated);
}

#endif