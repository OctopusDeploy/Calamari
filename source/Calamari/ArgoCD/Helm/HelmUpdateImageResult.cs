using System;
using System.Collections.Generic;
using Calamari.Kubernetes.Patching.JsonPatch;

namespace Calamari.ArgoCD.Helm
{
    public record HelmRefUpdatedResult(HashSet<string> ImagesUpdated, string RelativeFilepath, JsonPatchDocument JsonPatch);
}

