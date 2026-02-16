using System.Collections.Generic;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public List<PackageAndHelmReference> packageWithHelmReference { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<PackageAndHelmReference> packageWithHelmReference)
        {
            CommitParameters = commitParameters;
            this.packageWithHelmReference = packageWithHelmReference;
        }
    }
    
    public record PackageAndHelmReference(ContainerImageReference ImageReference, string? HelmReference);
}
