using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Helm
{
    public class HelmRefUpdatedResult
    {
        public HelmRefUpdatedResult(HashSet<string> imagesUpdated, string relativeFilepath)
        {
            ImagesUpdated = imagesUpdated;
            RelativeFilepath = relativeFilepath;
        }

        public HashSet<string> ImagesUpdated { get; }
        public string RelativeFilepath { get; }
    }
}

