namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public interface IResourceIdentity
    {
        string Kind { get; }
        string Name { get; }
        string Namespace { get; }
    }
}