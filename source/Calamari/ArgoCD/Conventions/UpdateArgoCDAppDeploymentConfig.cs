using System.Collections.Generic;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public IReadOnlyCollection<ContainerImageReferenceAndHelmReference> PackageWithHelmReference { get; }

        public bool UseHelmValueYamlPathFromStep { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<ContainerImageReferenceAndHelmReference> packageWithHelmReference, bool useHelmValueYamlPathFromStep)
        {
            CommitParameters = commitParameters;
            PackageWithHelmReference = packageWithHelmReference;
            UseHelmValueYamlPathFromStep = useHelmValueYamlPathFromStep;
        }
    }
    
    public record ContainerImageReferenceAndHelmReference(ContainerImageReference ContainerReference, string? HelmReference);
}
