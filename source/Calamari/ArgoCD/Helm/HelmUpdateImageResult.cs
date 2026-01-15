using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Helm
{
    public class HelmRefUpdatedResult
    {
        public HelmRefUpdatedResult(Uri repoUrl, HashSet<string> imagesUpdated)
        {
            RepoUrl = repoUrl;
            ImagesUpdated = imagesUpdated;
        }

        public Uri RepoUrl { get; }
        public HashSet<string> ImagesUpdated { get; }
    }
}

