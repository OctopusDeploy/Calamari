using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Models;

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
            const string Prefix = "argo.octopus.com";

            static readonly string OctopusProjectAnnotationKeyPrefix = $"{Prefix}/project";
            public static string OctopusProjectAnnotationKey(ApplicationSourceName? sourceName) => sourceName == null 
                ? OctopusProjectAnnotationKeyPrefix 
                : $"{OctopusProjectAnnotationKeyPrefix}.{sourceName}";

            static readonly string OctopusEnvironmentAnnotationKeyPrefix = $"{Prefix}/environment";
            public static string OctopusEnvironmentAnnotationKey(ApplicationSourceName? sourceName) => sourceName == null 
                ? OctopusEnvironmentAnnotationKeyPrefix 
                : $"{OctopusEnvironmentAnnotationKeyPrefix}.{sourceName}";

            static readonly string OctopusTenantAnnotationKeyPrefix = $"{Prefix}/tenant";
            public static string OctopusTenantAnnotationKey(ApplicationSourceName? sourceName) => sourceName == null 
                ? OctopusTenantAnnotationKeyPrefix 
                : $"{OctopusTenantAnnotationKeyPrefix}.{sourceName}";

            public static IReadOnlyCollection<string> GetUnnamedAnnotationKeys()
            {
                return new []
                {
                    OctopusProjectAnnotationKey(null),
                    OctopusEnvironmentAnnotationKey(null),
                    OctopusTenantAnnotationKey(null)
                };
            }
            
            public const string OctopusDefaultClusterRegistryAnnotationKey = "argo.octopus.com/default-container-registry";

            static readonly string OctopusImageReplacementPathsKeyPrefix = $"{Prefix}/image-replace-paths";

            public static string OctopusImageReplacementPathsKey(ApplicationSourceName? sourceName) => sourceName == null 
                ? OctopusImageReplacementPathsKeyPrefix 
                : $"{OctopusImageReplacementPathsKeyPrefix}.{sourceName}";

            // TODO: Verify that we need this. Here as a placeholder/reminder for now.
            // public const string OctopusStepIdAnnotationKey = "argo.octopus.com/step-id";

        }

        //TODO: AP - Note that these are the same as Argo
        public static readonly IReadOnlyCollection<string> KustomizationFileNames = new HashSet<string> { "kustomization.yaml", "kustomization.yml", "Kustomization" };
        
        public static readonly string HelmChartFileName = "Chart.yaml";
    }
}