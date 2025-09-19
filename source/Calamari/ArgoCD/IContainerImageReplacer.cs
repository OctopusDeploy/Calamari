#if NET
using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD
{
    public interface IContainerImageReplacer
    {
        ImageReplacementResult UpdateImages(List<ContainerImageReference> imagesToUpdate);
    }
}
#endif