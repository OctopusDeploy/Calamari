using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Helm
{
    public record HelmRefUpdatedResult(HashSet<string> ImagesUpdated, string RelativeFilepath, string JsonPatch);
}

