namespace Calamari.FeatureToggles
{

    /// <summary>
    /// This list of toggles should be a subset of the toggles supported by the current Octopus Server version in which it is published with.
    /// When a FeatureToggle no longer exists in server, it should be removed (along with relevant code) from this class
    /// </summary>
    public enum FeatureToggle {
        SkunkworksFeatureToggle,
        KubernetesDeploymentStatusFeatureToggle,
        GitSourcedYamlManifestsFeatureToggle
    }
}