using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppDeploymentConfig
    {
        public GitCommitParameters CommitParameters { get; }
        public List<ContainerImageReferenceAndHelmReference> ImageReferences { get; }

        public UpdateArgoCDAppDeploymentConfig(GitCommitParameters commitParameters, List<ContainerImageReferenceAndHelmReference> imageReferences)
        {
            CommitParameters = commitParameters;
            ImageReferences = imageReferences;
        }

        public bool HasStepBasedHelmValueReferences()
        {
            return ImageReferences.Any(ir => ir.HelmReference is not null);
        }
    }
}
