using System.Collections.Generic;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public List<ContainerImageReferenceAndHelmReference> PackageWithHelmReference { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<ContainerImageReferenceAndHelmReference> packageWithHelmReference)
        {
            CommitParameters = commitParameters;
            PackageWithHelmReference = packageWithHelmReference;
        }
    }
}
