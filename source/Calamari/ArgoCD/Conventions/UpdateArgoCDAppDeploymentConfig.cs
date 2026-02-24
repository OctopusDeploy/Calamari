using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public IReadOnlyCollection<ContainerImageReferenceAndHelmReference> PackageWithHelmReference { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<ContainerImageReferenceAndHelmReference> packageWithHelmReference)
        {
            CommitParameters = commitParameters;
            PackageWithHelmReference = packageWithHelmReference;
        }
    }
}
