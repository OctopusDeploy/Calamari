using System;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{
    public static class ArgoCDConstants
    {
        public const string DefaultContainerRegistry = "docker.io";

        // The ArgoCD API uses HEAD as a special value to indicate the default branch for an application.
        public const string HeadAsTarget = "HEAD";

        // For determining if a file may have ArgoCD application definitions
        public static readonly string[] SupportedAppFileExtensions = {".yaml", ".yml" };

    public class Annotations
        {
            public const string OctopusProjectAnnotationKey = "argo.octopus.com/project";
            public const string OctopusEnvironmentAnnotationKey = "argo.octopus.com/environment";
            public const string OctopusTenantAnnotationKey = "argo.octopus.com/tenant";

            public const string OctopusDefaultClusterRegistryAnnotationKey = "argo.octopus.com/default-container-registry";

            public const string OctopusImageReplacementPathsKey = "argo.octopus.com/image-replacement-paths";

            // TODO: Verify that we need this. Here as a placeholder/reminder for now.
            // public const string OctopusStepIdAnnotationKey = "argo.octopus.com/step-id";
        }
    }
}
