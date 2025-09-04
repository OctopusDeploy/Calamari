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
    }
}
