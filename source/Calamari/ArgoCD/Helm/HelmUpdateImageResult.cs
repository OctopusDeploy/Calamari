#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Helm
{
    public class HelmRefUpdatedResult
    {
        public HelmRefUpdatedResult(HashSet<string> imagesUpdated)
        {
            ImagesUpdated = imagesUpdated;
        }

        public HashSet<string> ImagesUpdated { get; }
    }
}

#endif