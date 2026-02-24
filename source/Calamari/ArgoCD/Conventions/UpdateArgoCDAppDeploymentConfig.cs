using System.Collections.Generic;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public List<ContainerImageReferenceAndHelmReference> ImageReferences { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<ContainerImageReferenceAndHelmReference> packageWithHelmReference)
        {
            CommitParameters = commitParameters;
            PackageWithHelmReference = packageWithHelmReference;
        }
    }
}
