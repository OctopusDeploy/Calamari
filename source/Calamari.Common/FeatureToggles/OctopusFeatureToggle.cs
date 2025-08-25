﻿using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class OctopusFeatureToggles
    {
        public static class KnownSlugs
        {
            public const string KubernetesObjectManifestInspection = "kubernetes-object-manifest-inspection";
            public const string KOSForHelm = "kos-for-helm";
            public const string ArgoCDCreatePullRequestFeatureToggle = "argocd-create-pull-request";
        };

        public static readonly OctopusFeatureToggle KubernetesObjectManifestInspectionFeatureToggle = new OctopusFeatureToggle(KnownSlugs.KubernetesObjectManifestInspection);
        public static readonly OctopusFeatureToggle KOSForHelmFeatureToggle = new OctopusFeatureToggle(KnownSlugs.KOSForHelm);
        public static readonly OctopusFeatureToggle ArgoCDCreatePullRequestFeatureToggle = new OctopusFeatureToggle(KnownSlugs.ArgoCDCreatePullRequestFeatureToggle);

        public class OctopusFeatureToggle
        {
            readonly string slug;

            public OctopusFeatureToggle(string slug)
            {
                this.slug = slug;
            }

            public bool IsEnabled(IVariables variables)
            {
                return variables.GetStrings(KnownVariables.EnabledFeatureToggles).Contains(slug);
            }
        }
    }
}