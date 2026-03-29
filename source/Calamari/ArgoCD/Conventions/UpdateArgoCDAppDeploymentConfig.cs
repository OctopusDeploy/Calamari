using System.Collections.Generic;
using System.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public List<ContainerImageReferenceAndHelmReference> ImageReferences { get; }
        public bool UseHelmReferenceFromContainer { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<ContainerImageReferenceAndHelmReference> imageReferences, bool useHelmReferenceFromContainer)
        {
            CommitParameters = commitParameters;
            ImageReferences = imageReferences;
            UseHelmReferenceFromContainer = useHelmReferenceFromContainer;
        }

        public bool HasStepBasedHelmValueReferences()
        {
            return ImageReferences.Any(ir => !ir.HelmReference.IsNullOrEmpty()) && UseHelmReferenceFromContainer;
        }
    }
}
