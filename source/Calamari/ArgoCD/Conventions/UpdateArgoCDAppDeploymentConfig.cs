using System.Collections.Generic;

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
}
