namespace Calamari.Kubernetes.ResourceStatus;

/// <summary>
/// Identifies a unique resource in a kubernetes cluster
/// </summary>
public class ResourceIdentifier
{
    // API version is irrelevant for identifying a resource,
    // since the resource name must be unique across all api versions.
    // https://kubernetes.io/docs/concepts/overview/working-with-objects/names/#names

    public string Kind { get; set; }
    public string Name { get; set; }
    public string Namespace { get; set; }
    public string Uid { get; set; }
}