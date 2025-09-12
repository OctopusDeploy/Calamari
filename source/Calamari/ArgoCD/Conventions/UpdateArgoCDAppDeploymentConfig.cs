#if NET
using System.Collections.Generic;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public List<ContainerImageReference> PackageReferences { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<ContainerImageReference> packageReferences)
        {
            CommitParameters = commitParameters;
            PackageReferences = packageReferences;
        }
    }
}
#endif