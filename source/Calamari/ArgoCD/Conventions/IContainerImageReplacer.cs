#if NET
using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;

namespace Calamari.ArgoCD.Conventions;

public interface IContainerImageReplacer
{
    ImageReplacementResult UpdateImages(List<ContainerImageReference> imagesToUpdate);
}
#endif