namespace Calamari.ResourceStatus;

/// <summary>
/// Identifies a unique resource in a kubernetes cluster
/// </summary>
public class ResourceIdentifier
{
    public string Kind { get; set; }
    public string Name { get; set; }
    public string Namespace { get; set; }
    public string Uid { get; set; }
}