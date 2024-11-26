namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public interface IResourceIdentity
    {
        string Group { get; }
        string Kind { get; }
        string Name { get; }
        string Namespace { get; }
    }
}