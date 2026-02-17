using System.Collections.Generic;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public IReadOnlyCollection<PackageAndHelmReference> PackageWithHelmReference { get; }

        public bool UseHelmValueYamlPathFromStep { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<PackageAndHelmReference> packageWithHelmReference, bool useHelmValueYamlPathFromStep)
        {
            CommitParameters = commitParameters;
            PackageWithHelmReference = packageWithHelmReference;
            UseHelmValueYamlPathFromStep = useHelmValueYamlPathFromStep;
        }
    }
    
    public record PackageAndHelmReference(ContainerImageReference ContainerReference, string? HelmReference);
}
