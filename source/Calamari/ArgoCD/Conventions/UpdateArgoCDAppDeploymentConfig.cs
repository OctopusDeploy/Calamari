#if NET
using System.Collections.Generic;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public List<ContainerImageReference> ImageReferences { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<ContainerImageReference> imageReferences)
        {
            CommitParameters = commitParameters;
            ImageReferences = imageReferences;
        }
    }
}
#endif