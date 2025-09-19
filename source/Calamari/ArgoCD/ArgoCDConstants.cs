#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD
{

    public static class ArgoCDConstants
    {
        public const string DefaultContainerRegistry = "docker.io";

        // The ArgoCD API uses HEAD as a special value to indicate the default branch for an application.
        public const string HeadAsTarget = "HEAD";

        // For determining if a file may have ArgoCD application definitions
        public static readonly string[] SupportedAppFileExtensions = { ".yaml", ".yml" };

        // Used to specify root path notation where Ref sources are used
        public const string RefSourcePath = "./";

        public class Annotations
        {
            public const string OctopusProjectAnnotationKey = "argo.octopus.com/project";
            public const string OctopusEnvironmentAnnotationKey = "argo.octopus.com/environment";
            public const string OctopusTenantAnnotationKey = "argo.octopus.com/tenant";

            public const string OctopusDefaultClusterRegistryAnnotationKey = "argo.octopus.com/default-container-registry";

            public const string OctopusImageReplacementPathsKey = "argo.octopus.com/image-replace-paths";

            public const string OctopusImageReplaceAliasKey = "argo.octopus.com/image-replace-alias";

            public static string OctopusImageReplacementPathsKeyWithSpecifier(string specifier) => $"{OctopusImageReplacementPathsKey}.{specifier}";


            // TODO: Verify that we need this. Here as a placeholder/reminder for now.
            // public const string OctopusStepIdAnnotationKey = "argo.octopus.com/step-id";

        }

        //TODO: AP - Note that these are the same as Argo
        public static readonly IReadOnlySet<string> KustomizationFileNames = new HashSet<string> { "kustomization.yaml", "kustomization.yml", "Kustomization" };
    }
}
#endif