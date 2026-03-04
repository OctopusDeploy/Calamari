using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD
{
    public interface IContainerImageReplacer
    {
        ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate);
    }
}